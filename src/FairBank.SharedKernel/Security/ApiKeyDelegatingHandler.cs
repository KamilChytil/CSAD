using Microsoft.Extensions.Configuration;

namespace FairBank.SharedKernel.Security;

/// <summary>
/// <see cref="DelegatingHandler"/> that attaches the <c>X-Internal-Api-Key</c> header
/// to every outbound HTTP request made by service-to-service typed clients.
///
/// Register in DI as a transient handler and chain with <c>.AddHttpMessageHandler</c>:
/// <code>
/// services.AddTransient&lt;ApiKeyDelegatingHandler&gt;();
/// services.AddHttpClient("some-client", ...)
///         .AddHttpMessageHandler&lt;ApiKeyDelegatingHandler&gt;();
/// </code>
/// </summary>
public sealed class ApiKeyDelegatingHandler : DelegatingHandler
{
    private readonly string? _apiKey;

    public ApiKeyDelegatingHandler(IConfiguration configuration)
    {
        _apiKey = configuration[ApiKeyMiddleware.ConfigKey];
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.TryAddWithoutValidation(ApiKeyMiddleware.HeaderName, _apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
