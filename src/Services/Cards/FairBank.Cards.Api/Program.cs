using FairBank.Cards.Api.Endpoints;
using FairBank.Cards.Application;
using FairBank.Cards.Infrastructure;
using FairBank.Cards.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FairBank.SharedKernel.Security;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddCardsApplication();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

builder.Services.AddCardsInfrastructure(connectionString);
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-create database tables on startup.
// EnsureCreatedAsync() does NOT create tables when the database already exists (shared DB).
// We check whether our tables exist and create them from the EF model if missing.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using (var checkCmd = conn.CreateCommand())
    {
        checkCmd.CommandText =
            "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema = 'cards_service' AND table_name = 'cards')";
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
    app.MapScalarApiReference();
}

// Validate X-Internal-Api-Key on every inbound request (gateway → service auth)
app.UseMiddleware<ApiKeyMiddleware>();

app.UseSerilogRequestLogging();

app.MapCardEndpoints();

app.MapGet("/health", async (CardsDbContext db) =>
{
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new { Status = "Healthy", Service = "Cards" });
    }
    catch
    {
        return Results.Json(new { Status = "Unhealthy", Service = "Cards" }, statusCode: 503);
    }
}).WithTags("Health");

app.Run();

public partial class Program;
