using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using FairBank.ApiGateway.Middleware;

namespace FairBank.ApiGateway.Tests.Middleware;

public class AuthMiddlewareEdgeCaseTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthMiddleware> _logger;
    private bool _nextDelegateCalled;

    public AuthMiddlewareEdgeCaseTests()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<AuthMiddleware>>();
    }

    private AuthMiddleware CreateMiddleware(RequestDelegate? next = null)
    {
        next ??= _ =>
        {
            _nextDelegateCalled = true;
            return Task.CompletedTask;
        };

        return new AuthMiddleware(next, _httpClientFactory, _cache, _logger);
    }

    private static string CreateValidToken(Guid userId, Guid sessionId)
    {
        var raw = $"{userId}:{sessionId}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    private void SetupIdentityServiceWithHandler(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://identity-api:8080")
        };
        _httpClientFactory.CreateClient("IdentityService").Returns(httpClient);
    }

    private void SetupIdentityServiceResponse(HttpStatusCode statusCode, object? body = null)
    {
        var responseMessage = new HttpResponseMessage(statusCode);
        if (body is not null)
        {
            responseMessage.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        var handler = new FakeHttpMessageHandler(responseMessage);
        SetupIdentityServiceWithHandler(handler);
    }

    private static string ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return reader.ReadToEnd();
    }

    // ── Invalid session NOT cached ──────────────────────────────

    [Fact]
    public async Task InvokeAsync_InvalidSession_NotCached_SubsequentRequestCallsIdentityAgain()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var token = CreateValidToken(userId, sessionId);

        var callCount = 0;

        // Handler that always returns isValid=false and counts calls
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { isValid = false, userId = (string?)null, role = (string?)null }),
                    Encoding.UTF8,
                    "application/json")
            },
            () => callCount++);

        SetupIdentityServiceWithHandler(handler);

        var middleware = CreateMiddleware();

        // First request – invalid session
        var context1 = new DefaultHttpContext();
        context1.Request.Path = "/api/v1/accounts/123";
        context1.Request.Headers.Authorization = $"Bearer {token}";
        context1.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context1);

        context1.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        // Second request with same token – should NOT use cache, should call Identity again
        _nextDelegateCalled = false;
        var context2 = new DefaultHttpContext();
        context2.Request.Path = "/api/v1/accounts/456";
        context2.Request.Headers.Authorization = $"Bearer {token}";
        context2.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context2);

        callCount.Should().Be(2, "invalid sessions must not be cached so each request should call the Identity service");
    }

    // ── Token edge cases ────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_TokenWithExtraColons_Returns401()
    {
        // Base64("guid:guid:extra") has 3 parts when split by ':'
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var raw = $"{userId}:{sessionId}:extra";
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));

        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_TokenWithEmptyGuids_TriesToValidate()
    {
        // Empty GUIDs are technically valid GUID format
        var userId = Guid.Empty;
        var sessionId = Guid.Empty;
        var token = CreateValidToken(userId, sessionId);

        // Set up identity to return valid – proving the middleware does try to validate
        SetupIdentityServiceResponse(HttpStatusCode.OK, new
        {
            isValid = true,
            userId = userId.ToString(),
            role = "Client"
        });

        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = $"Bearer {token}";

        await middleware.InvokeAsync(context);

        // The middleware should have passed through to next (it tried to validate)
        _nextDelegateCalled.Should().BeTrue("empty GUIDs are valid GUID format and should be sent for validation");
        context.Request.Headers["X-User-Id"].ToString().Should().Be(userId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_VeryLongToken_Returns401Gracefully()
    {
        // 10 KB of random bytes base64-encoded – not a valid "guid:guid" format
        var longBytes = new byte[10 * 1024];
        Random.Shared.NextBytes(longBytes);
        var token = Convert.ToBase64String(longBytes);

        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_TokenWithWhitespacePadding_IsHandled()
    {
        // Bearer header with extra whitespace around the token
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var token = CreateValidToken(userId, sessionId);

        SetupIdentityServiceResponse(HttpStatusCode.OK, new
        {
            isValid = true,
            userId = userId.ToString(),
            role = "Client"
        });

        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        // Add leading/trailing whitespace around the token value
        context.Request.Headers.Authorization = $"Bearer   {token}   ";

        await middleware.InvokeAsync(context);

        // ExtractBearerToken calls .Trim(), so the token should be extracted and validated
        _nextDelegateCalled.Should().BeTrue("whitespace around the token should be trimmed");
        context.Request.Headers["X-User-Id"].ToString().Should().Be(userId.ToString());
    }

    // ── Anonymous path edge cases ───────────────────────────────

    [Fact]
    public async Task InvokeAsync_AnonymousPathWithQueryString_StillAnonymous()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/users/login";
        context.Request.QueryString = new QueryString("?redirect=/home");

        await middleware.InvokeAsync(context);

        _nextDelegateCalled.Should().BeTrue("query strings should not affect anonymous path matching");
    }

    [Fact]
    public async Task InvokeAsync_AnonymousPathMixedCaseWithExtraSegments_StillMatches()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        // Mixed case + extra segments – StartsWith with OrdinalIgnoreCase should match
        context.Request.Path = "/API/V1/USERS/LOGIN/extra";

        await middleware.InvokeAsync(context);

        _nextDelegateCalled.Should().BeTrue("StartsWith with OrdinalIgnoreCase should match mixed-case paths with extra segments");
    }

    // ── Identity service error handling ─────────────────────────

    [Fact]
    public async Task InvokeAsync_IdentityServiceTimeout_Returns401()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var token = CreateValidToken(userId, sessionId);

        var handler = new ThrowingHttpMessageHandler(
            new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));
        SetupIdentityServiceWithHandler(handler);

        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_IdentityServiceReturnsMalformedJson_Returns401()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var token = CreateValidToken(userId, sessionId);

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("this is not valid json {{{", Encoding.UTF8, "application/json")
        };
        var handler = new FakeHttpMessageHandler(responseMessage);
        SetupIdentityServiceWithHandler(handler);

        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_IdentityServiceReturns200WithEmptyBody_Returns401()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var token = CreateValidToken(userId, sessionId);

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };
        var handler = new FakeHttpMessageHandler(responseMessage);
        SetupIdentityServiceWithHandler(handler);

        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    // ── Response format ─────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer !!!invalid!!!")]
    public async Task InvokeAsync_ErrorResponse_HasApplicationJsonContentType(string? authHeader)
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        if (authHeader is not null)
        {
            context.Request.Headers.Authorization = authHeader;
        }
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Be("application/json");
    }

    [Theory]
    [InlineData(null, "Missing or invalid authorization token.")]
    [InlineData("Bearer !!!invalid!!!", "Invalid token format.")]
    public async Task InvokeAsync_ErrorResponse_ContainsErrorField(string? authHeader, string expectedError)
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        if (authHeader is not null)
        {
            context.Request.Headers.Authorization = authHeader;
        }
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        var body = ReadResponseBody(context);
        var json = JsonSerializer.Deserialize<JsonElement>(body);

        json.TryGetProperty("error", out var errorProp).Should().BeTrue("error response body should contain an 'error' field");
        errorProp.GetString().Should().Be(expectedError);
    }

    [Fact]
    public async Task InvokeAsync_InvalidSession_ErrorResponse_HasApplicationJsonContentType()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var token = CreateValidToken(userId, sessionId);

        SetupIdentityServiceResponse(HttpStatusCode.OK, new
        {
            isValid = false,
            userId = (string?)null,
            role = (string?)null
        });

        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Be("application/json");

        var body = ReadResponseBody(context);
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        json.TryGetProperty("error", out var errorProp).Should().BeTrue();
        errorProp.GetString().Should().Be("Session is invalid or expired.");
    }
}

/// <summary>
/// A fake HttpMessageHandler that throws a specified exception on SendAsync.
/// Used to simulate timeouts and other transport-level failures.
/// </summary>
internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHttpMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw _exception;
    }
}
