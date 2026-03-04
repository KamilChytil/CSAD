using Microsoft.JSInterop;

namespace FairBank.Web.Shared.Services;

public sealed class ThemeService(IJSRuntime js)
{
    // Must match the key used in index.html inline <script>
    private const string StorageKey = "vb-theme";

    public bool IsDarkMode { get; private set; }

    public event Action? OnThemeChanged;

    public async Task InitializeAsync()
    {
        try
        {
            var saved = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            IsDarkMode = saved == "dark";
            await ApplyThemeAsync();
        }
        catch { /* JS interop may not be available during prerendering */ }
    }

    public async Task SetDarkModeAsync(bool isDark)
    {
        IsDarkMode = isDark;
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, isDark ? "dark" : "light");
            await ApplyThemeAsync();
        }
        catch { }
        OnThemeChanged?.Invoke();
    }

    public async Task ToggleAsync()
    {
        await SetDarkModeAsync(!IsDarkMode);
    }

    private async Task ApplyThemeAsync()
    {
        try
        {
            // CSS uses [data-theme="dark"] selectors — must set/remove the attribute
            if (IsDarkMode)
                await js.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", "dark");
            else
                await js.InvokeVoidAsync("document.documentElement.removeAttribute", "data-theme");
        }
        catch { }
    }
}
