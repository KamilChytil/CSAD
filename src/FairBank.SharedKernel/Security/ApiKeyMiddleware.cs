using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairBank.SharedKernel.Security;

/// <summary>
/// Middleware that validates the <c>X-Internal-Api-Key</c> header on every incoming request.
/// Health-check and OpenAPI/Scalar paths are exempted so that Docker healthchecks
/// and development tools keep working without the key.
///
/// Register in each micro-service's <c>Program.cs</c> with:
/// <code>app.UseMiddleware&lt;ApiKeyMiddleware&gt;();</code>
///
/// The expected key is read from <c>Security:InternalApiKey</c> in the service's configuration
/// (typically supplied via an environment variable in docker-compose).
/// </summary>
public sealed class ApiKeyMiddleware
{
    public const string HeaderName = "X-Internal-Api-Key";
    public const string ConfigKey = "Security:InternalApiKey";

    private readonly RequestDelegate _next;
    private readonly string? _expectedKey;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    private static readonly string[] ExemptPrefixes =
    [
        "/health",
        "/openapi",
        "/scalar"
    ];

    public ApiKeyMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _expectedKey = configuration[ConfigKey];
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Allow health-checks and dev tooling through without a key.
        if (IsExempt(path))
        {
            await _next(context);
            return;
        }

        // If no key is configured the middleware is effectively disabled (development).
        if (string.IsNullOrEmpty(_expectedKey))
        {
            await _next(context);
            return;
        }

        var providedKey = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(providedKey) ||
            !string.Equals(providedKey, _expectedKey, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Rejected request to {Path} — missing or invalid {Header}",
                path, HeaderName);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Forbidden – invalid internal API key." }));
            return;
        }

        await _next(context);
    }

    private static bool IsExempt(string path)
    {
        foreach (var prefix in ExemptPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
