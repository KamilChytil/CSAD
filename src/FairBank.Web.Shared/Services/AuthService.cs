using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FairBank.Web.Shared.Models;
using Microsoft.JSInterop;

namespace FairBank.Web.Shared.Services;

public sealed class AuthService(HttpClient http, IJSRuntime js) : IAuthService, IDisposable
{
    private const string SessionStorageKey = "fairbank_session";
    // NOTE: Lockout is NOT stored in localStorage. It lives only in server-side DB
    // (User.LockedUntil) and in-memory on the frontend (_lockedUntil). Clearing
    // browser storage cannot bypass lockout — the server returns HTTP 429.

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

    /// <inheritdoc />
    public bool WasSessionExpired { get; private set; }

    public async Task InitializeAsync()
    {
        await LoadSessionAsync();

        if (_currentSession is not null)
        {
            if (_currentSession.ExpiresAt <= DateTime.UtcNow)
            {
                WasSessionExpired = true;
                await ClearSessionAsync();
            }
            else
            {
                // Enforce single-session: check server to see if this session is still active.
                // If another browser logged in, the server will return false here.
                var valid = await ValidateSessionAsync();
                if (!valid)
                {
                    WasSessionExpired = true;
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
        // Fast in-memory check — avoids sending request while we know the lockout hasn't expired.
        // This is NOT a security control. Even if bypassed, the server returns 429 from DB state.
        if (IsLocked)
            return null;

        try
        {
            var response = await http.PostAsJsonAsync("api/v1/auth/login", request);

            // 429 = server-side lockout — read the unlock time from the response body.
            // Stored in-memory only. Clearing localStorage does NOT bypass this.
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var lockout = await response.Content.ReadFromJsonAsync<LoginLockoutResponse>();
                if (lockout is not null)
                    _lockedUntil = lockout.LockedUntil;
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

            // Success — clear any in-memory lockout cache.
            _lockedUntil = null;
            WasSessionExpired = false;

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
            // Network error — fail closed. Banking app must not allow access
            // when it cannot verify the session with the server.
            return false;
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
        WasSessionExpired = true;
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

    // Lockout is NOT persisted in localStorage — it exists only in server DB
    // (User.LockedUntil) and in-memory (_lockedUntil) for UI display.
    // Clearing browser storage does NOT bypass lockout.
}
