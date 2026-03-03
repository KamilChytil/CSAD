using FairBank.Web.Shared.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<FairBank.Web.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<IFairBankApi, FairBankApiClient>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped(sp => new FairBank.Web.Shared.Services.Chat.ChatService(
    sp.GetRequiredService<HttpClient>(), 
    builder.HostEnvironment.BaseAddress));

builder.Services.AddSingleton<AuthStateService>();

var host = builder.Build();

// Obnovit stav přihlášení z localStorage před prvním vykreslením
var authState = host.Services.GetRequiredService<AuthStateService>();
var js = host.Services.GetRequiredService<IJSRuntime>();
await authState.InitializeAsync(js);

await host.RunAsync();
