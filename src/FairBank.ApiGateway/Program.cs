using FairBank.SharedKernel;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.Kafka(
        ctx.Configuration["Kafka:BootstrapServers"] ?? "kafka:9092",
        ctx.Configuration["Kafka:Topic"] ?? "fairbank-logs"));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapReverseProxy();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ApiGateway" }));

app.Run();
