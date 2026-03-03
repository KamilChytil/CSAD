using FairBank.Identity.Api.Endpoints;
using FairBank.Identity.Application;
using FairBank.Identity.Infrastructure;
using FairBank.SharedKernel;
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

// Application layer (MediatR, FluentValidation)
builder.Services.AddIdentityApplication();

// Infrastructure layer (EF Core, repositories)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
builder.Services.AddIdentityInfrastructure(connectionString);

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();

// Map endpoints
app.MapUserEndpoints();

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Identity" }))
    .WithTags("Health");

app.Run();

// Required for integration tests
public partial class Program;
