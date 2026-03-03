using FairBank.Accounts.Api.Endpoints;
using FairBank.Accounts.Application;
using FairBank.Accounts.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddAccountsApplication();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
builder.Services.AddAccountsInfrastructure(connectionString);

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

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Accounts" }))
    .WithTags("Health");

app.Run();

public partial class Program;
