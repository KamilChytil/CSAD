using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FairBank.SharedKernel.Security;

public static class AuthorizationExtensions
{
    public static TBuilder RequireAuth<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var userId = context.HttpContext.Request.Headers["X-User-Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();
            return await next(context);
        });

    public static TBuilder RequireRole<TBuilder>(this TBuilder builder, params string[] roles) where TBuilder : IEndpointConventionBuilder
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var userRole = context.HttpContext.Request.Headers["X-User-Role"].FirstOrDefault();
            if (string.IsNullOrEmpty(userRole) || !roles.Contains(userRole, StringComparer.OrdinalIgnoreCase))
                return Results.Json(new { error = "Forbidden" }, statusCode: 403);
            return await next(context);
        });

    public static Guid? GetUserId(this HttpContext httpContext)
        => Guid.TryParse(httpContext.Request.Headers["X-User-Id"].FirstOrDefault(), out var uid) ? uid : null;

    public static string? GetUserRole(this HttpContext httpContext)
        => httpContext.Request.Headers["X-User-Role"].FirstOrDefault();
}
