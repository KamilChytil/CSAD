using FairBank.Web.Shared.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<FairBank.Web.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp =>
    new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
        Timeout = TimeSpan.FromSeconds(15)
    });

builder.Services.AddScoped<IFairBankApi, FairBankApiClient>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped(sp => new FairBank.Web.Shared.Services.Chat.ChatService(
    sp.GetRequiredService<HttpClient>(), 
    builder.HostEnvironment.BaseAddress));

builder.Services.AddScoped<ThemeService>();

var host = builder.Build();

// Inicializovat theme service
var themeService = host.Services.GetRequiredService<ThemeService>();
await themeService.InitializeAsync();

await host.RunAsync();
