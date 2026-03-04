using FluentAssertions;
using Microsoft.AspNetCore.Http;
using FairBank.SharedKernel.Security;

namespace FairBank.SharedKernel.Tests.Security;

public class AuthorizationFiltersTests
{
    // ── GetUserId ────────────────────────────────────────────

    [Fact]
    public void GetUserId_WithValidGuidHeader_ReturnsGuid()
    {
        var expected = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = expected.ToString();

        var result = httpContext.GetUserId();

        result.Should().Be(expected);
    }

    [Fact]
    public void GetUserId_WithMissingHeader_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();

        var result = httpContext.GetUserId();

        result.Should().BeNull();
    }

    [Fact]
    public void GetUserId_WithInvalidGuidHeader_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = "not-a-guid";

        var result = httpContext.GetUserId();

        result.Should().BeNull();
    }

    [Fact]
    public void GetUserId_WithEmptyHeader_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = string.Empty;

        var result = httpContext.GetUserId();

        result.Should().BeNull();
    }

    // ── GetUserRole ──────────────────────────────────────────

    [Fact]
    public void GetUserRole_WithHeaderPresent_ReturnsRoleString()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Role"] = "Admin";

        var result = httpContext.GetUserRole();

        result.Should().Be("Admin");
    }

    [Fact]
    public void GetUserRole_WithMissingHeader_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();

        var result = httpContext.GetUserRole();

        result.Should().BeNull();
    }

    [Fact]
    public void GetUserRole_WithEmptyHeader_ReturnsEmpty()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Role"] = string.Empty;

        var result = httpContext.GetUserRole();

        // StringValues with "" yields "" from FirstOrDefault, not null
        result.Should().BeEmpty();
    }
}
