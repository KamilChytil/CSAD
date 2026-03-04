using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using FairBank.ApiGateway.Middleware;

namespace FairBank.ApiGateway.Tests.Middleware;

public class RateLimitingMiddlewareTests
{
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private bool _nextDelegateCalled;

    public RateLimitingMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<RateLimitingMiddleware>>();
    }

    private RateLimitingMiddleware CreateMiddleware(RequestDelegate? next = null)
    {
        next ??= _ =>
        {
            _nextDelegateCalled = true;
            return Task.CompletedTask;
        };

        return new RateLimitingMiddleware(next, _logger);
    }

    // ── Exact path → policy mapping ──────────────────────────

    [Theory]
    [InlineData("/api/v1/users/login", "auth")]
    [InlineData("/api/v1/users/register", "auth")]
    [InlineData("/api/v1/users/2fa/verify", "auth")]
    [InlineData("/api/v1/users/forgot-password", "sensitive")]
    [InlineData("/api/v1/users/reset-password", "sensitive")]
    [InlineData("/api/v1/users/resend-verification", "sensitive")]
    public async Task InvokeAsync_KnownExactPath_SetsCorrectPolicy(string path, string expectedPolicy)
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        context.Items["RateLimitPolicy"].Should().Be(expectedPolicy);
        _nextDelegateCalled.Should().BeTrue();
    }

    // ── Default/global policy ────────────────────────────────

    [Theory]
    [InlineData("/api/v1/accounts/123")]
    [InlineData("/api/v1/payments/transfer")]
    [InlineData("/health")]
    [InlineData("/some-unknown-path")]
    public async Task InvokeAsync_UnknownPath_SetsGlobalPolicy(string path)
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        context.Items["RateLimitPolicy"].Should().Be("global");
        _nextDelegateCalled.Should().BeTrue();
    }

    // ── Case insensitivity ───────────────────────────────────

    [Theory]
    [InlineData("/API/V1/USERS/LOGIN", "auth")]
    [InlineData("/Api/V1/Users/Register", "auth")]
    [InlineData("/API/V1/USERS/FORGOT-PASSWORD", "sensitive")]
    public async Task InvokeAsync_CaseInsensitivePath_MatchesCorrectPolicy(string path, string expectedPolicy)
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        context.Items["RateLimitPolicy"].Should().Be(expectedPolicy);
    }

    // ── Trailing slash ───────────────────────────────────────

    [Theory]
    [InlineData("/api/v1/users/login/", "auth")]
    [InlineData("/api/v1/users/forgot-password/", "sensitive")]
    public async Task InvokeAsync_TrailingSlash_MatchesCorrectPolicy(string path, string expectedPolicy)
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        context.Items["RateLimitPolicy"].Should().Be(expectedPolicy);
    }

    // ── Next delegate is always called ───────────────────────

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNextDelegate()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/users/login";

        await middleware.InvokeAsync(context);

        _nextDelegateCalled.Should().BeTrue();
    }

    // ── Null/empty path ──────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_EmptyPath_SetsGlobalPolicy()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = string.Empty;

        await middleware.InvokeAsync(context);

        context.Items["RateLimitPolicy"].Should().Be("global");
    }
}
