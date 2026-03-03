using System.Net.Http.Json;
using System.Text.Json;
using FairBank.Web.Shared.Models;
using Microsoft.JSInterop;

namespace FairBank.Web.Shared.Services;

public sealed class AuthService(HttpClient http, IJSRuntime js) : IAuthService, IDisposable
{
    private const string SessionStorageKey = "fairbank_session";
    private const string AttemptsStorageKey = "fairbank_login_attempts";
    private const string LockedUntilStorageKey = "fairbank_locked_until";

    private const int MaxLoginAttempts = 5;
    private const int LockoutMinutes = 5;
    private const int InactivityTimeoutMinutes = 5;

    private AuthSession? _currentSession;
    private int _failedAttempts;
    private DateTime? _lockedUntil;
    private System.Threading.Timer? _inactivityTimer;

    public AuthSession? CurrentSession => _currentSession;
    public bool IsAuthenticated => _currentSession is not null && _currentSession.ExpiresAt > DateTime.UtcNow;
    public int RemainingAttempts => Math.Max(0, MaxLoginAttempts - _failedAttempts);
    public DateTime? LockedUntil => _lockedUntil;
    public bool IsLocked => _lockedUntil.HasValue && _lockedUntil.Value > DateTime.UtcNow;

    public event Action? AuthStateChanged;

    public async Task InitializeAsync()
    {
        await LoadLockoutStateAsync();
        await LoadSessionAsync();

        if (_currentSession is not null)
        {
            if (_currentSession.ExpiresAt <= DateTime.UtcNow)
            {
                await ClearSessionAsync();
            }
            else
            {
                StartInactivityTimer();
            }
        }

        AuthStateChanged?.Invoke();
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        await LoadLockoutStateAsync();

        if (IsLocked)
            return null;

        try
        {
            var response = await http.PostAsJsonAsync("api/v1/auth/login", request);

            if (!response.IsSuccessStatusCode)
            {
                _failedAttempts++;
                if (_failedAttempts >= MaxLoginAttempts)
                {
                    _lockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                    await SaveLockoutStateAsync();
                }
                else
                {
                    await SaveLockoutStateAsync();
                }

                AuthStateChanged?.Invoke();
                return null;
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (loginResponse is null) return null;

            _failedAttempts = 0;
            _lockedUntil = null;
            await SaveLockoutStateAsync();

            _currentSession = new AuthSession(
                loginResponse.SessionId,
                loginResponse.UserId,
                loginResponse.Token,
                loginResponse.Email,
                loginResponse.FirstName,
                loginResponse.LastName,
                loginResponse.Role,
                loginResponse.ExpiresAt);

            await SaveSessionAsync();
            StartInactivityTimer();
            AuthStateChanged?.Invoke();

            return loginResponse;
        }
        catch
        {
            _failedAttempts++;
            if (_failedAttempts >= MaxLoginAttempts)
            {
                _lockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
            }

            await SaveLockoutStateAsync();
            AuthStateChanged?.Invoke();
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        if (_currentSession is not null)
        {
            try
            {
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _currentSession.Token);
                await http.PostAsync("api/v1/auth/logout", null);
            }
            catch
            {
                // Server nedostupný — lokální odhlášení pokračuje
            }
            finally
            {
                http.DefaultRequestHeaders.Authorization = null;
            }
        }

        StopInactivityTimer();
        await ClearSessionAsync();
        AuthStateChanged?.Invoke();
    }

    public async Task<UserResponse?> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await http.PostAsJsonAsync("api/v1/auth/register", request);

            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<UserResponse>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ValidateSessionAsync()
    {
        if (_currentSession is null) return false;

        try
        {
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _currentSession.Token);

            var response = await http.GetAsync($"api/v1/auth/session/{_currentSession.SessionId}");

            if (!response.IsSuccessStatusCode)
            {
                await LogoutAsync();
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (_currentSession is not null)
            {
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _currentSession.Token);
            }
        }
    }

    public void ResetInactivityTimer()
    {
        if (_currentSession is null) return;
        StopInactivityTimer();
        StartInactivityTimer();
    }

    public void Dispose()
    {
        StopInactivityTimer();
    }

    // ── Private helpers ─────────────────────────────────────

    private void StartInactivityTimer()
    {
        StopInactivityTimer();
        _inactivityTimer = new System.Threading.Timer(
            _ => _ = OnInactivityTimeoutAsync(),
            null,
            TimeSpan.FromMinutes(InactivityTimeoutMinutes),
            Timeout.InfiniteTimeSpan);
    }

    private void StopInactivityTimer()
    {
        _inactivityTimer?.Dispose();
        _inactivityTimer = null;
    }

    private async Task OnInactivityTimeoutAsync()
    {
        StopInactivityTimer();
        await ClearSessionAsync();
        AuthStateChanged?.Invoke();
    }

    private async Task SaveSessionAsync()
    {
        if (_currentSession is null) return;
        var json = JsonSerializer.Serialize(_currentSession);
        await js.InvokeVoidAsync("localStorage.setItem", SessionStorageKey, json);
    }

    private async Task LoadSessionAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", SessionStorageKey);
            if (string.IsNullOrEmpty(json)) return;
            _currentSession = JsonSerializer.Deserialize<AuthSession>(json);
        }
        catch
        {
            _currentSession = null;
        }
    }

    private async Task ClearSessionAsync()
    {
        _currentSession = null;
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", SessionStorageKey);
        }
        catch
        {
            // JS runtime nemusí být dostupný při dispose
        }
    }

    private async Task SaveLockoutStateAsync()
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", AttemptsStorageKey, _failedAttempts.ToString());

            if (_lockedUntil.HasValue)
            {
                await js.InvokeVoidAsync("localStorage.setItem", LockedUntilStorageKey,
                    _lockedUntil.Value.ToString("O"));
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.removeItem", LockedUntilStorageKey);
            }
        }
        catch
        {
            // Ignorovat chyby při ukládání stavu
        }
    }

    private async Task LoadLockoutStateAsync()
    {
        try
        {
            var attemptsStr = await js.InvokeAsync<string?>("localStorage.getItem", AttemptsStorageKey);
            _failedAttempts = int.TryParse(attemptsStr, out var a) ? a : 0;

            var lockedStr = await js.InvokeAsync<string?>("localStorage.getItem", LockedUntilStorageKey);
            if (!string.IsNullOrEmpty(lockedStr) && DateTime.TryParse(lockedStr, out var locked))
            {
                _lockedUntil = locked > DateTime.UtcNow ? locked : null;
                if (_lockedUntil is null)
                {
                    _failedAttempts = 0;
                    await SaveLockoutStateAsync();
                }
            }
            else
            {
                _lockedUntil = null;
            }
        }
        catch
        {
            _failedAttempts = 0;
            _lockedUntil = null;
        }
    }
}
