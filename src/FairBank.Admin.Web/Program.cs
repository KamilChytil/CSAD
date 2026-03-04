using FairBank.Admin.Web.Services;
using FairBank.Admin.Web.Data;
using FairBank.Web.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Scalar.AspNetCore;
using FairBank.Admin.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add services to the container.
builder.Services.AddOpenApi();

// Add DbContext for Log Persistence
builder.Services.AddPooledDbContextFactory<LogDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("LogsConnection") ?? "Data Source=logs.db"));

// Add Kafka Consumer Background Service
builder.Services.AddSingleton<KafkaLogConsumerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<KafkaLogConsumerService>());

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
    });

builder.Services.AddAuthorization();

// ── HTTP client for API Gateway communication ──
var apiGatewayUrl = builder.Configuration["Services:ApiGateway"] ?? "http://api-gateway:8080";
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiGatewayUrl),
    Timeout = TimeSpan.FromSeconds(15)
});

// ── Shared services (FairBankApi, Auth, Theme) ──
builder.Services.AddScoped<IFairBankApi, FairBankApiClient>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ThemeService>();

var app = builder.Build();

// Ensure Database is Created
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LogDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.EnsureCreated();
}

app.UseStaticFiles();
app.UseAntiforgery();

// Minimal API Endpoints
app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/api/logs", async (
    IDbContextFactory<LogDbContext> dbFactory,
    string? search,
    string? level,
    string? service,
    int limit = 100) =>
{
    using var db = dbFactory.CreateDbContext();
    var query = db.Logs.AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
        query = query.Where(l => l.Message.Contains(search));

    if (!string.IsNullOrWhiteSpace(level))
        query = query.Where(l => l.Level == level);

    if (!string.IsNullOrWhiteSpace(service))
        query = query.Where(l => l.Service == service);

    return await query.OrderByDescending(l => l.Timestamp)
                      .Take(limit)
                      .ToListAsync();
})
.WithName("GetLogs");

app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", service = "AdminApi" }));

// Blazor Server UI
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
