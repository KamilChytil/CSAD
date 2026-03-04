using FairBank.Documents.Application;
using FairBank.Documents.Api.Endpoints;
using FairBank.Documents.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddDocumentsApplication();

var accountsUrl = builder.Configuration["Services:AccountsApi"]
    ?? throw new InvalidOperationException("Missing Services:AccountsApi setting");
builder.Services.AddDocumentsInfrastructure(accountsUrl);

builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();
app.MapStatementEndpoints();
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Documents" })).WithTags("Health");
app.Run();

public partial class Program;