using System.Text.Json;
using System.Threading.RateLimiting;
using FairBank.ApiGateway.Middleware;
using FairBank.SharedKernel;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.Kafka(
        ctx.Configuration["Kafka:BootstrapServers"] ?? "kafka:9092",
        ctx.Configuration["Kafka:Topic"] ?? "fairbank-logs"));

// ---------------------------------------------------------------------------
// Rate Limiting
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Custom rejection response with JSON body and Retry-After header.
    options.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfterSeconds = 60;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = (int)retryAfter.TotalSeconds;
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        context.HttpContext.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new
        {
            error = "Too many requests",
            retryAfter = retryAfterSeconds
        });

        await context.HttpContext.Response.WriteAsync(body, cancellationToken);
    };

    // "global" policy - 200 requests per minute per IP (permissive for general use).
    options.AddPolicy("global", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 200,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // "auth" policy - 5 requests per minute per IP (strict for login/register/2FA).
    options.AddPolicy("auth", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // "sensitive" policy - 10 requests per minute per IP (for password reset, email verification).
    options.AddPolicy("sensitive", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // Global limiter selects the correct policy based on the request path.
    // The path-based middleware (UsePathBasedRateLimiting) sets the policy name
    // in HttpContext.Items before this limiter runs.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var policyName = httpContext.Items["RateLimitPolicy"] as string ?? "global";
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = $"{policyName}_{ip}";

        return policyName switch
        {
            "auth" => RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }),

            "sensitive" => RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }),

            _ => RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                })
        };
    });
});

builder.Services.AddHealthChecks();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// In-memory cache for session validation results (30s TTL, avoids per-request Identity calls)
builder.Services.AddMemoryCache();

// HttpClient for calling the Identity service session-validate endpoint
builder.Services.AddHttpClient("IdentityService", client =>
{
    var identityBaseUrl = builder.Configuration["IdentityService:BaseUrl"] ?? "http://identity-api:8080";
    client.BaseAddress = new Uri(identityBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("GatewayCors", policy =>
        policy.WithOrigins(
                "http://localhost",
                "http://localhost:80",
                "https://localhost",
                "https://localhost:443",
                "http://web-app",
                "http://web-app:80")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors("GatewayCors");

// Path-based middleware resolves the rate limit policy name BEFORE the rate limiter runs.
app.UsePathBasedRateLimiting();

// Built-in rate limiter - must be BEFORE routing/YARP.
app.UseRateLimiter();

// Auth middleware validates session tokens BEFORE YARP forwards the request.
// Sets X-User-Id and X-User-Role headers for downstream services.
app.UseMiddleware<AuthMiddleware>();

app.MapReverseProxy();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ApiGateway" }));

app.Run();
