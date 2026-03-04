using FairBank.Products.Api.Endpoints;
using FairBank.Products.Application;
using FairBank.Products.Infrastructure;
using FairBank.Products.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddProductsApplication();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

builder.Services.AddProductsInfrastructure(connectionString);
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-create database schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();

app.MapProductApplicationEndpoints();

app.MapGet("/health", async (ProductsDbContext db) =>
{
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new { Status = "Healthy", Service = "Products" });
    }
    catch
    {
        return Results.Json(new { Status = "Unhealthy", Service = "Products" }, statusCode: 503);
    }
}).WithTags("Health");

app.Run();

public partial class Program;
