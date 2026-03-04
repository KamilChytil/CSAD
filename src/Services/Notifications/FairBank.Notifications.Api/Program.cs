using FairBank.Notifications.Api.Endpoints;
using FairBank.Notifications.Application;
using FairBank.Notifications.Application.Hubs;
using FairBank.Notifications.Infrastructure;
using FairBank.Notifications.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddNotificationsApplication();
builder.Services.AddNotificationsInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials());
});

var app = builder.Build();

// Ensure DB schema exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => { options.Title = "FairBank Notifications API"; });
}

app.UseCors("AllowAll");

// ── Health ─────────────────────────────────────────────────────────────────
app.MapGet("/health", () => new { Status = "Healthy", Service = "Notifications" });

// ── Notification Endpoints ─────────────────────────────────────────────────
app.MapNotificationEndpoints();

// ── SignalR Hub ────────────────────────────────────────────────────────────
app.MapHub<NotificationHub>("/notification-hub");

app.Run();
