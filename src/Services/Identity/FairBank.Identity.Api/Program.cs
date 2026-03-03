using FairBank.Identity.Api.Configuration;
using FairBank.Identity.Api.Endpoints;
using FairBank.Identity.Api.Seeders;
using FairBank.Identity.Application;
using FairBank.Identity.Infrastructure;
using FairBank.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

// Application layer (MediatR, FluentValidation)
builder.Services.AddIdentityApplication();

// Infrastructure layer (EF Core, repositories)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
builder.Services.AddIdentityInfrastructure(connectionString);

// Admin seeder options
builder.Services.Configure<AdminSeederSettings>(
    builder.Configuration.GetSection("AdminSeeder"));

// Serialize enums as strings so frontend receives "Admin" instead of 3
builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply EF Core migrations on startup
using (var migrationScope = app.Services.CreateScope())
{
    var db = migrationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

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

// Seed default admin user
await AdminSeeder.SeedAsync(app.Services);

app.Run();

// Required for integration tests
public partial class Program;
