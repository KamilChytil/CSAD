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

public class AuthMiddlewareTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthMiddleware> _logger;
    private bool _nextDelegateCalled;

    public AuthMiddlewareTests()
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
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://identity-api:8080")
        };

        _httpClientFactory.CreateClient("IdentityService").Returns(httpClient);
    }

    // ── Anonymous paths ──────────────────────────────────────

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/users/login")]
    [InlineData("/api/v1/users/register")]
    [InlineData("/api/v1/users/forgot-password")]
    [InlineData("/api/v1/users/reset-password")]
    [InlineData("/api/v1/users/verify-email")]
    [InlineData("/api/v1/users/resend-verification")]
    [InlineData("/api/v1/users/2fa/verify")]
    [InlineData("/scalar")]
    [InlineData("/openapi")]
    [InlineData("/identity/health")]
    [InlineData("/accounts/health")]
    [InlineData("/payments/health")]
    public async Task InvokeAsync_AnonymousPath_PassesThroughWithoutAuth(string path)
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        _nextDelegateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AnonymousPath_CaseInsensitive_PassesThrough()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/HEALTH";

        await middleware.InvokeAsync(context);

        _nextDelegateCalled.Should().BeTrue();
    }

    // ── Missing Authorization header ─────────────────────────

    [Fact]
    public async Task InvokeAsync_MissingAuthorizationHeader_Returns401()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_EmptyBearerToken_Returns401()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = "Bearer ";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_NonBearerScheme_Returns401()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = "Basic dXNlcjpwYXNz";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    // ── Invalid token format ─────────────────────────────────

    [Fact]
    public async Task InvokeAsync_InvalidBase64Token_Returns401()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = "Bearer !!!not-base64!!!";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_TokenWithoutColonSeparator_Returns401()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes("no-colon-here"));
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_TokenWithInvalidGuids_Returns401()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes("not-a-guid:also-not-a-guid"));
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    // ── Session validation success ───────────────────────────

    [Fact]
    public async Task InvokeAsync_ValidTokenAndSession_SetsUserHeaders()
    {
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
        context.Request.Headers.Authorization = $"Bearer {token}";

        await middleware.InvokeAsync(context);

        context.Request.Headers["X-User-Id"].ToString().Should().Be(userId.ToString());
        context.Request.Headers["X-User-Role"].ToString().Should().Be("Client");
        _nextDelegateCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ValidTokenAndSession_CallsNextDelegate()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var token = CreateValidToken(userId, sessionId);

        SetupIdentityServiceResponse(HttpStatusCode.OK, new
        {
            isValid = true,
            userId = userId.ToString(),
            role = "Admin"
        });

        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = $"Bearer {token}";

        await middleware.InvokeAsync(context);

        _nextDelegateCalled.Should().BeTrue();
    }

    // ── Session validation failure ───────────────────────────

    [Fact]
    public async Task InvokeAsync_InvalidSession_Returns401()
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

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_IdentityServiceReturnsError_Returns401()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var token = CreateValidToken(userId, sessionId);

        SetupIdentityServiceResponse(HttpStatusCode.InternalServerError);

        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _nextDelegateCalled.Should().BeFalse();
    }

    // ── Caching behavior ─────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_SecondRequestWithSameToken_UsesCachedResult()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var token = CreateValidToken(userId, sessionId);

        var callCount = 0;
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { isValid = true, userId = userId.ToString(), role = "Client" }),
                Encoding.UTF8,
                "application/json")
        };

        var handler = new FakeHttpMessageHandler(responseMessage, () => callCount++);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://identity-api:8080")
        };
        _httpClientFactory.CreateClient("IdentityService").Returns(httpClient);

        var middleware = CreateMiddleware();

        // First request
        var context1 = new DefaultHttpContext();
        context1.Request.Path = "/api/v1/accounts/123";
        context1.Request.Headers.Authorization = $"Bearer {token}";
        await middleware.InvokeAsync(context1);

        // Second request with same token
        _nextDelegateCalled = false;
        var context2 = new DefaultHttpContext();
        context2.Request.Path = "/api/v1/accounts/456";
        context2.Request.Headers.Authorization = $"Bearer {token}";
        await middleware.InvokeAsync(context2);

        callCount.Should().Be(1, "the second request should use the cached result");
        _nextDelegateCalled.Should().BeTrue();
    }

    // ── JSON error response ──────────────────────────────────

    [Fact]
    public async Task InvokeAsync_Unauthorized_WritesJsonErrorBody()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/accounts/123";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);

        json.GetProperty("error").GetString().Should().NotBeNullOrEmpty();
        context.Response.ContentType.Should().Be("application/json");
    }
}

/// <summary>
/// A simple fake HttpMessageHandler that returns a pre-configured response.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;
    private readonly Action? _onSend;

    public FakeHttpMessageHandler(HttpResponseMessage response, Action? onSend = null)
    {
        _response = response;
        _onSend = onSend;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _onSend?.Invoke();
        return Task.FromResult(_response);
    }
}
