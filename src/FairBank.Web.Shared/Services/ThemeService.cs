using Microsoft.JSInterop;

namespace FairBank.Web.Shared.Services;

public sealed class ThemeService
{
    private readonly IJSRuntime _js;
    private const string StorageKey = "fairbank_theme";

    public bool IsDarkMode { get; private set; }
    public event Action? OnThemeChanged;

    public ThemeService(IJSRuntime js) => _js = js;

    public async Task InitializeAsync()
    {
        try
        {
            var stored = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            IsDarkMode = stored == "dark";
            await ApplyThemeAsync();
        }
        catch { /* SSR / prerender — ignore */ }
    }

    public async Task ToggleAsync()
    {
        await SetDarkModeAsync(!IsDarkMode);
    }

    public async Task SetDarkModeAsync(bool isDark)
    {
        IsDarkMode = isDark;
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, IsDarkMode ? "dark" : "light");
        await ApplyThemeAsync();
        OnThemeChanged?.Invoke();
    }

    private async Task ApplyThemeAsync()
    {
        try
        {
            var theme = IsDarkMode ? "dark" : "light";
            await _js.InvokeVoidAsync("eval", $"document.documentElement.setAttribute('data-theme','{theme}')");
        }
        catch { /* ignore */ }
    }
}
