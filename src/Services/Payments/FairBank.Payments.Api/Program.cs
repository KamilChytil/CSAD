using FairBank.Payments.Api.Endpoints;
using FairBank.Payments.Application;
using FairBank.Payments.Infrastructure;
using FairBank.Payments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FairBank.SharedKernel.Security;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddPaymentsApplication();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

var accountsApiUrl = builder.Configuration["Services:AccountsApi"]
    ?? "http://accounts-api:8080";

var identityApiUrl = builder.Configuration["Services:IdentityApi"]
    ?? "http://identity-api:8080";

builder.Services.AddHttpContextAccessor();
builder.Services.AddPaymentsInfrastructure(connectionString, accountsApiUrl, identityApiUrl);
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-create database tables on startup.
// EnsureCreatedAsync() does NOT create tables when the database already exists (shared DB).
// We check whether our tables exist and create them from the EF model if missing.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using (var checkCmd = conn.CreateCommand())
    {
        checkCmd.CommandText =
            "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema = 'payments_service' AND table_name = 'exchange_transactions')";
        var exists = (bool)(await checkCmd.ExecuteScalarAsync())!;
        if (!exists)
        {
            // Replace CREATE TABLE with CREATE TABLE IF NOT EXISTS to safely handle
            // partially-created schemas (e.g. after a failed or partial migration).
            var script = db.Database.GenerateCreateScript()
                .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.Ordinal);
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

app.MapPaymentEndpoints();
app.MapStandingOrderEndpoints();
app.MapPaymentTemplateEndpoints();
app.MapExchangeEndpoints();

app.MapGet("/health", async (PaymentsDbContext db) =>
{
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new { Status = "Healthy", Service = "Payments" });
    }
    catch
    {
        return Results.Json(new { Status = "Unhealthy", Service = "Payments" }, statusCode: 503);
    }
}).WithTags("Health");

app.Run();

public partial class Program;
