using FairBank.Web.Shared.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace FairBank.Web.Shared.Services;

public sealed class AuthStateService
{
    private const string StorageKey = "fairbank_user";

    private UserResponse? _currentUser;

    public event Action? OnChanged;

    public UserResponse? CurrentUser => _currentUser;

    public bool IsAuthenticated => _currentUser is not null;

    public bool IsAdmin => _currentUser?.Role == "Admin";

    public bool IsBanker => _currentUser?.Role == "Banker";

    public bool IsChild => _currentUser?.Role == "Child";

    /// <summary>Admin or Banker — staff that can review product applications.</summary>
    public bool IsStaff => IsAdmin || IsBanker;

    public async Task InitializeAsync(IJSRuntime js)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(json))
                _currentUser = JsonSerializer.Deserialize<UserResponse>(json);
        }
        catch
        {
            // localStorage nedostupný (testy apod.)
        }
    }

    public async Task LoginAsync(UserResponse user, IJSRuntime js)
    {
        _currentUser = user;
        try
        {
            var json = JsonSerializer.Serialize(user);
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch { }
        OnChanged?.Invoke();
    }

    public async Task LogoutAsync(IJSRuntime js)
    {
        _currentUser = null;
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch { }
        OnChanged?.Invoke();
    }
}
