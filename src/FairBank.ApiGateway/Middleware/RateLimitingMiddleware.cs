namespace FairBank.ApiGateway.Middleware;

/// <summary>
/// Middleware that applies path-based rate limiting policies to incoming requests.
/// Since YARP dynamically maps endpoints, this middleware inspects the request path
/// and applies the appropriate rate limiter before the request reaches YARP.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    private static readonly Dictionary<string, string> ExactPathPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/api/v1/users/login"] = "auth",
        ["/api/v1/users/register"] = "auth",
        ["/api/v1/users/forgot-password"] = "sensitive",
        ["/api/v1/users/reset-password"] = "sensitive",
        ["/api/v1/users/2fa/verify"] = "auth",
        ["/api/v1/users/resend-verification"] = "sensitive",
    };

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var policyName = ResolvePolicyName(path);

        // Store the resolved policy name so the built-in rate limiter can use it.
        context.Items["RateLimitPolicy"] = policyName;

        _logger.LogDebug(
            "Rate limiting: path={Path}, policy={Policy}, ip={Ip}",
            path,
            policyName,
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        await _next(context);
    }

    private static string ResolvePolicyName(string path)
    {
        // Check exact path matches first (case-insensitive).
        if (ExactPathPolicies.TryGetValue(path.TrimEnd('/'), out var policy))
        {
            return policy;
        }

        return "global";
    }
}

/// <summary>
/// Extension methods for registering the rate limiting middleware.
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UsePathBasedRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}
