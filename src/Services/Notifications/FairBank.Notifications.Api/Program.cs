using FairBank.Notifications.Api.Endpoints;
using FairBank.Notifications.Application;
using FairBank.Notifications.Application.Hubs;
using FairBank.Notifications.Infrastructure;
using FairBank.Notifications.Infrastructure.Persistence;
using FairBank.SharedKernel;
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

// Auto-create database tables on startup.
// EnsureCreatedAsync() does NOT create tables when the database already exists (shared DB).
// We check whether our tables exist and create them from the EF model if missing.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using (var checkCmd = conn.CreateCommand())
    {
        checkCmd.CommandText =
            "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema = 'notifications_service' AND table_name = 'notifications')";
        var exists = (bool)(await checkCmd.ExecuteScalarAsync())!;
        if (!exists)
        {
            var script = db.Database.GenerateCreateScript();
            await using var createCmd = conn.CreateCommand();
            createCmd.CommandText = script;
            await createCmd.ExecuteNonQueryAsync();
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => { options.Title = "FairBank Notifications API"; });
}

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
