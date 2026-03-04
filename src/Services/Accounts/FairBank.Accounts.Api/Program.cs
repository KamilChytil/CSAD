using System.Text.Json.Serialization;
using FairBank.Accounts.Api.Endpoints;
using FairBank.Accounts.Api.Seeders;
using FairBank.Accounts.Application;
using FairBank.Accounts.Infrastructure;
using FairBank.SharedKernel;
using Npgsql;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serialize enums as strings (e.g. "CZK" instead of 0)
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.Kafka(
        ctx.Configuration["Kafka:BootstrapServers"] ?? "kafka:9092",
        ctx.Configuration["Kafka:Topic"] ?? "fairbank-logs"));

builder.Services.AddAccountsApplication();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
var identityApiUrl = builder.Configuration["Services:IdentityApi"] ?? "http://identity-api:8080";
builder.Services.AddAccountsInfrastructure(connectionString, identityApiUrl);

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();

app.MapAccountEndpoints();
app.MapCardEndpoints();
app.MapSavingsGoalEndpoints();
app.MapSavingsRuleEndpoints();
app.MapInvestmentEndpoints();

app.MapGet("/health", async (IConfiguration config) =>
{
    try
    {
        var cs = config.GetConnectionString("DefaultConnection");
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        return Results.Ok(new { Status = "Healthy", Service = "Accounts" });
    }
    catch
    {
        return Results.Json(new { Status = "Unhealthy", Service = "Accounts" }, statusCode: 503);
    }
}).WithTags("Health");

// Seed demo accounts (idempotent — skips if already exist)
await AccountSeeder.SeedAsync(app.Services);

app.Run();

public partial class Program;
