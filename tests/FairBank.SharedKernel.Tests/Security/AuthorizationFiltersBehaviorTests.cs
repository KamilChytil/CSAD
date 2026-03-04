using FluentAssertions;
using Microsoft.AspNetCore.Http;
using FairBank.SharedKernel.Security;

namespace FairBank.SharedKernel.Tests.Security;

public class AuthorizationFiltersBehaviorTests
{
    // ── GetUserId edge cases ───────────────────────────────────

    [Fact]
    public void GetUserId_WithWhitespaceOnlyHeader_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = "   ";

        var result = httpContext.GetUserId();

        result.Should().BeNull();
    }

    [Fact]
    public void GetUserId_WithUppercaseGuid_ReturnsCorrectValue()
    {
        var expected = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = expected.ToString().ToUpperInvariant();

        var result = httpContext.GetUserId();

        result.Should().Be(expected);
    }

    [Fact]
    public void GetUserId_WithBracedGuid_ReturnsCorrectValue()
    {
        var expected = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = $"{{{expected}}}";

        var result = httpContext.GetUserId();

        result.Should().Be(expected);
    }

    // ── GetUserRole edge cases ─────────────────────────────────

    [Fact]
    public void GetUserRole_WithWhitespaceHeader_ReturnsWhitespace()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Role"] = "   ";

        var result = httpContext.GetUserRole();

        // FirstOrDefault returns the value as-is; it does not trim
        result.Should().Be("   ");
    }

    [Fact]
    public void GetUserRole_WithMultipleHeaders_ReturnsFirst()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Append("X-User-Role", "Client");
        httpContext.Request.Headers.Append("X-User-Role", "Admin");

        var result = httpContext.GetUserRole();

        // StringValues.FirstOrDefault returns the first value
        result.Should().Be("Client");
    }

    [Theory]
    [InlineData("Client")]
    [InlineData("Banker")]
    [InlineData("Admin")]
    [InlineData("Parent")]
    [InlineData("Child")]
    public void GetUserRole_WithCommonRoles_ReturnsCorrectValue(string role)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Role"] = role;

        var result = httpContext.GetUserRole();

        result.Should().Be(role);
    }

    // ── Header injection prevention ────────────────────────────

    [Theory]
    [InlineData("12345678-1234-1234-1234-123456789abc\r\nX-User-Role: Admin")]
    [InlineData("12345678-1234-1234-1234-123456789abc\nEvil-Header: value")]
    [InlineData("12345678-1234-1234-1234-123456789abc\n injected")]
    public void GetUserId_WithNewlineCharacters_ReturnsNull(string maliciousValue)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = maliciousValue;

        var result = httpContext.GetUserId();

        // Guid.TryParse rejects trailing newlines/injected content
        result.Should().BeNull();
    }

    [Fact]
    public void GetUserRole_WithVeryLongString_ReturnsItWithoutCrash()
    {
        var longRole = new string('A', 10_000);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Role"] = longRole;

        var result = httpContext.GetUserRole();

        result.Should().Be(longRole);
    }

    // ── BOLA pattern tests ─────────────────────────────────────
    //
    // Many endpoints follow a common Broken Object Level Authorization
    // pattern: compare the authenticated user's ID against the resource
    // owner's ID, with role-based bypasses. These tests validate that
    // pattern in isolation.

    private static bool IsBolaPassing(Guid? authUserId, string? authRole, Guid requestedUserId)
    {
        if (authRole == "Admin") return true;
        if (authRole == "Banker") return true;
        return authUserId == requestedUserId;
    }

    [Fact]
    public void Bola_SameUserId_Passes()
    {
        var userId = Guid.NewGuid();

        var result = IsBolaPassing(userId, "Client", userId);

        result.Should().BeTrue();
    }

    [Fact]
    public void Bola_DifferentUserId_ClientRole_Fails()
    {
        var authUserId = Guid.NewGuid();
        var requestedUserId = Guid.NewGuid();

        var result = IsBolaPassing(authUserId, "Client", requestedUserId);

        result.Should().BeFalse();
    }

    [Fact]
    public void Bola_AdminRole_BypassesCheck()
    {
        var authUserId = Guid.NewGuid();
        var requestedUserId = Guid.NewGuid();

        var result = IsBolaPassing(authUserId, "Admin", requestedUserId);

        result.Should().BeTrue();
    }

    [Fact]
    public void Bola_NullAuthUserId_Fails()
    {
        var requestedUserId = Guid.NewGuid();

        var result = IsBolaPassing(null, "Client", requestedUserId);

        result.Should().BeFalse();
    }

    [Fact]
    public void Bola_ChildAccessingParentResources_Fails()
    {
        var childId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        var result = IsBolaPassing(childId, "Child", parentId);

        result.Should().BeFalse();
    }
}
