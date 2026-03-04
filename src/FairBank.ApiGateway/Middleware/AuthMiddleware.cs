using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace FairBank.ApiGateway.Middleware;

/// <summary>
/// Authentication middleware that validates session tokens against the Identity service
/// before forwarding requests to downstream microservices via YARP.
///
/// Token format: Bearer {Base64("userId:sessionId")}
/// On success, sets X-User-Id and X-User-Role headers for downstream services.
/// Caches validation results in-memory for 30 seconds to reduce Identity service load.
/// </summary>
public sealed class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthMiddleware> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Paths that do not require authentication.
    /// Compared case-insensitively against the beginning of the request path.
    /// </summary>
    private static readonly string[] AnonymousPaths =
    [
        "/health",
        "/api/v1/users/login",
        "/api/v1/auth/login",
        "/api/v1/users/register",
        "/api/v1/auth/register",
        "/api/v1/users/forgot-password",
        "/api/v1/auth/forgot-password",
        "/api/v1/users/reset-password",
        "/api/v1/auth/reset-password",
        "/api/v1/users/verify-email",
        "/api/v1/auth/verify-email",
        "/api/v1/users/resend-verification",
        "/api/v1/auth/resend-verification",
        "/api/v1/users/2fa/verify",
        "/api/v1/auth/2fa/verify",
        "/scalar",
        "/openapi"
    ];

    /// <summary>
    /// Path prefixes for health-check routes forwarded to downstream services.
    /// These are also anonymous.
    /// </summary>
    private static readonly string[] AnonymousHealthPrefixes =
    [
        "/identity/health",
        "/accounts/health",
        "/payments/health"
    ];

    public AuthMiddleware(
        RequestDelegate next,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<AuthMiddleware> logger)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (IsAnonymousPath(path))
        {
            await _next(context);
            return;
        }

        var token = ExtractBearerToken(context);
        if (token is null)
        {
            _logger.LogDebug("Missing or malformed Authorization header for {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await WriteJsonError(context, "Missing or invalid authorization token.");
            return;
        }

        if (!TryDecodeToken(token, out var userId, out var sessionId))
        {
            _logger.LogDebug("Failed to decode session token for {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await WriteJsonError(context, "Invalid token format.");
            return;
        }

        var validationResult = await ValidateSessionAsync(userId, sessionId);
        if (validationResult is null || !validationResult.IsValid)
        {
            _logger.LogDebug("Session validation failed for user {UserId} on {Path}", userId, path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await WriteJsonError(context, "Session is invalid or expired.");
            return;
        }

        // Attach identity info as headers for downstream services
        context.Request.Headers["X-User-Id"] = validationResult.UserId;
        context.Request.Headers["X-User-Role"] = validationResult.Role;

        await _next(context);
    }

    // ── Private helpers ─────────────────────────────────────────

    private static bool IsAnonymousPath(string path)
    {
        foreach (var anonPath in AnonymousPaths)
        {
            if (path.StartsWith(anonPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var prefix in AnonymousHealthPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? ExtractBearerToken(HttpContext context)
    {
        var auth = context.Request.Headers.Authorization.FirstOrDefault();
        if (auth is null || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = auth["Bearer ".Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }

    /// <summary>
    /// Decodes the Base64-encoded session token into userId and sessionId.
    /// Mirrors the logic in SessionTokenHelper from the Identity service.
    /// </summary>
    private static bool TryDecodeToken(string token, out Guid userId, out Guid sessionId)
    {
        userId = default;
        sessionId = default;

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = raw.Split(':');
            if (parts.Length != 2) return false;
            if (!Guid.TryParse(parts[0], out userId)) return false;
            if (!Guid.TryParse(parts[1], out sessionId)) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates the session against the Identity service, using an in-memory cache
    /// with a 30-second TTL to reduce downstream calls.
    /// </summary>
    private async Task<SessionValidationResult?> ValidateSessionAsync(Guid userId, Guid sessionId)
    {
        var cacheKey = $"session:{userId}:{sessionId}";

        if (_cache.TryGetValue(cacheKey, out SessionValidationResult? cached))
        {
            return cached;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("IdentityService");
            var url = $"/api/v1/users/{userId}/session-validate?sessionId={sessionId}";

            using var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Identity service returned {StatusCode} for session validation of user {UserId}",
                    response.StatusCode, userId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SessionValidationResult>(json, JsonOptions);

            if (result is not null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                };

                // Only cache valid sessions; don't cache invalid ones to allow
                // immediate retry after re-login.
                if (result.IsValid)
                {
                    _cache.Set(cacheKey, result, cacheOptions);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate session for user {UserId}", userId);
            return null;
        }
    }

    private static async Task WriteJsonError(HttpContext context, string message)
    {
        context.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(body);
    }
}

/// <summary>
/// Represents the response from the Identity service session-validate endpoint.
/// </summary>
internal sealed class SessionValidationResult
{
    public bool IsValid { get; set; }
    public string? UserId { get; set; }
    public string? Role { get; set; }
}
