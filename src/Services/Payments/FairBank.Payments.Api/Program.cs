using FairBank.Payments.Api.Endpoints;
using FairBank.Payments.Application;
using FairBank.Payments.Infrastructure;
using FairBank.Payments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddPaymentsInfrastructure(connectionString, accountsApiUrl);
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-create database schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();

app.MapPaymentEndpoints();
app.MapStandingOrderEndpoints();
app.MapPaymentTemplateEndpoints();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Payments" }))
    .WithTags("Health");

app.Run();

public partial class Program;
