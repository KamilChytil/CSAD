using Microsoft.JSInterop;

namespace FairBank.Web.Shared.Services;

/// <summary>
/// Blazor service wrapping the vabank.theme JS interop for dark/light mode management.
/// Register as Singleton in DI.
/// </summary>
public sealed class ThemeService(IJSRuntime js) : IAsyncDisposable
{
    private bool _isDarkMode;
    private bool _initialized;

    public bool IsDarkMode => _isDarkMode;
    public event Action? OnThemeChanged;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        try
        {
            var theme = await js.InvokeAsync<string>("vabank.theme.get");
            _isDarkMode = theme == "dark";
            _initialized = true;
        }
        catch
        {
            // JS interop not ready yet
        }
    }

    public async Task ToggleAsync()
    {
        try
        {
            var newTheme = await js.InvokeAsync<string>("vabank.theme.toggle");
            _isDarkMode = newTheme == "dark";
            OnThemeChanged?.Invoke();
        }
        catch
        {
            // Fallback
        }
    }

    public async Task SetDarkModeAsync(bool isDark)
    {
        try
        {
            await js.InvokeVoidAsync("vabank.theme.set", isDark ? "dark" : "light");
            _isDarkMode = isDark;
            OnThemeChanged?.Invoke();
        }
        catch
        {
            // Fallback  
        }
    }

    public ValueTask DisposeAsync()
    {
        OnThemeChanged = null;
        return ValueTask.CompletedTask;
    }
}
