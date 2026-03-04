using FairBank.Notifications.Api.Endpoints;
using FairBank.Notifications.Application;
using FairBank.Notifications.Application.Hubs;
using FairBank.Notifications.Infrastructure;
using FairBank.Notifications.Infrastructure.Persistence;
using FairBank.SharedKernel;
using FairBank.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.Kafka(
        ctx.Configuration["Kafka:BootstrapServers"] ?? "kafka:9092",
        ctx.Configuration["Kafka:Topic"] ?? "fairbank-logs"));

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddNotificationsApplication();
builder.Services.AddNotificationsInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
        policy.WithOrigins(
                "http://localhost",
                "http://localhost:80",
                "https://localhost",
                "https://localhost:443",
                "http://web-app",
                "http://web-app:80",
                "http://api-gateway",
                "http://api-gateway:8080")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// Auto-create database tables on startup (idempotent).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    try
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        var script = db.Database.GenerateCreateScript()
            .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.Ordinal)
            .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ", StringComparison.Ordinal)
            .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ", StringComparison.Ordinal)
            .Replace("CREATE SEQUENCE ", "CREATE SEQUENCE IF NOT EXISTS ", StringComparison.Ordinal);

        await using var createCmd = conn.CreateCommand();
        createCmd.CommandText = script;
        await createCmd.ExecuteNonQueryAsync();
    }
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07")
    {
        // Tables/indexes already exist – safe to ignore.
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => { options.Title = "FairBank Notifications API"; });
}

// Validate X-Internal-Api-Key on every inbound request (gateway → service auth)
app.UseMiddleware<ApiKeyMiddleware>();

app.UseCors("AllowSpecificOrigins");

app.UseSerilogRequestLogging();

// ── Health ─────────────────────────────────────────────────────────────────
app.MapGet("/health", async (NotificationsDbContext db) =>
{
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new { Status = "Healthy", Service = "Notifications" });
    }
    catch
    {
        return Results.Json(new { Status = "Unhealthy", Service = "Notifications" }, statusCode: 503);
    }
});

// ── Notification Endpoints ─────────────────────────────────────────────────
app.MapNotificationEndpoints();

// ── SignalR Hub ────────────────────────────────────────────────────────────
app.MapHub<NotificationHub>("/notification-hub");

app.Run();
