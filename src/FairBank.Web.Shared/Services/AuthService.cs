using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FairBank.Web.Shared.Models;
using Microsoft.JSInterop;

namespace FairBank.Web.Shared.Services;

public sealed class AuthService(HttpClient http, IJSRuntime js) : IAuthService, IDisposable
{
    private const string SessionStorageKey = "fairbank_session";
    private const string LockedUntilStorageKey = "fairbank_locked_until";

    private const int InactivityTimeoutMinutes = 5;

    private AuthSession? _currentSession;
    private DateTime? _lockedUntil;
    private System.Threading.Timer? _inactivityTimer;

    public AuthSession? CurrentSession => _currentSession;
    public bool IsAuthenticated => _currentSession is not null && _currentSession.ExpiresAt > DateTime.UtcNow;
    /// <summary>Not tracked client-side — lockout is server-enforced. Kept for UI display only.</summary>
    public int RemainingAttempts => 5; // server handles this
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
                // Enforce single-session: check server to see if this session is still active.
                // If another browser logged in, the server will return false here.
                var valid = await ValidateSessionAsync();
                if (!valid)
                {
                    await ClearSessionAsync();
                }
                else
                {
                    StartInactivityTimer();
                }
            }
        }

        AuthStateChanged?.Invoke();
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        await LoadLockoutStateAsync();

        // Fast local check — avoids sending request while we know the lockout hasn't expired.
        if (IsLocked)
            return null;

        try
        {
            var response = await http.PostAsJsonAsync("api/v1/auth/login", request);

            // 429 = server-side lockout — read the unlock time from the response body.
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var lockout = await response.Content.ReadFromJsonAsync<LoginLockoutResponse>();
                if (lockout is not null)
                {
                    _lockedUntil = lockout.LockedUntil;
                    await SaveLockoutStateAsync();
                }
                AuthStateChanged?.Invoke();
                return null;
            }

            // 401 = wrong credentials only (no client-side counter — server handles attempts).
            if (!response.IsSuccessStatusCode)
            {
                AuthStateChanged?.Invoke();
                return null;
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (loginResponse is null) return null;

            // Success — clear any cached lockout.
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
            var response = await http.PostAsJsonAsync("api/v1/users/register", request);

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
        if (_currentSession.ExpiresAt <= DateTime.UtcNow) return false;

        // Ask the server whether this session is still the active one.
        // This enforces single-session: if another browser logged in, this returns false.
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/users/session/validate");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentSession.Token);
            var resp = await http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            // Network error — optimistically allow (avoids offline false-logout)
            return true;
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
            if (_lockedUntil.HasValue)
                await js.InvokeVoidAsync("localStorage.setItem", LockedUntilStorageKey, _lockedUntil.Value.ToString("O"));
            else
                await js.InvokeVoidAsync("localStorage.removeItem", LockedUntilStorageKey);
        }
        catch { }
    }

    private async Task LoadLockoutStateAsync()
    {
        try
        {
            var lockedStr = await js.InvokeAsync<string?>("localStorage.getItem", LockedUntilStorageKey);
            if (!string.IsNullOrEmpty(lockedStr) && DateTime.TryParse(lockedStr, out var locked))
                _lockedUntil = locked > DateTime.UtcNow ? locked : null;
            else
                _lockedUntil = null;

            // Auto-clear expired lockout from localStorage
            if (_lockedUntil is null && !string.IsNullOrEmpty(lockedStr))
                await js.InvokeVoidAsync("localStorage.removeItem", LockedUntilStorageKey);
        }
        catch
        {
            _lockedUntil = null;
        }
    }
}
