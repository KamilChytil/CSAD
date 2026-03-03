using Microsoft.JSInterop;

namespace FairBank.Web.Shared.Services;

public class ThemeService
{
    private readonly IJSRuntime _js;
    private const string StorageKey = "vb-theme";
    public bool IsDarkMode { get; private set; }

    public event Action? OnThemeChanged;

    public ThemeService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var theme = await _js.InvokeAsync<string>("localStorage.getItem", StorageKey);
            IsDarkMode = theme == "dark";
            // No need to ApplyTheme here if index.html handles it, but let's be safe
            await ApplyThemeToDocument();
        }
        catch { }
    }

    public async Task ToggleAsync()
    {
        await SetDarkModeAsync(!IsDarkMode);
    }

    public async Task SetDarkModeAsync(bool isDark)
    {
        IsDarkMode = isDark;
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, isDark ? "dark" : "light");
            await ApplyThemeToDocument();
            OnThemeChanged?.Invoke();
        }
        catch { }
    }

    private async Task ApplyThemeToDocument()
    {
        try
        {
            await _js.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", IsDarkMode ? "dark" : "light");
        }
        catch { }
    }
}
