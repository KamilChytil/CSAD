using Microsoft.AspNetCore.Http;

namespace FairBank.Payments.Infrastructure.HttpClients;

public sealed class AuthHeaderForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            if (httpContext.Request.Headers.TryGetValue("X-User-Id", out var userId))
                request.Headers.TryAddWithoutValidation("X-User-Id", userId.ToString());
            if (httpContext.Request.Headers.TryGetValue("X-User-Role", out var userRole))
                request.Headers.TryAddWithoutValidation("X-User-Role", userRole.ToString());
        }
        return base.SendAsync(request, cancellationToken);
    }
}
