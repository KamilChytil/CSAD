using FluentAssertions;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.ValueObjects;

namespace FairBank.Identity.UnitTests.Domain;

public class UserSecurityTests
{
    private User CreateUser()
    {
        return User.Create("Jan", "Novák", Email.Create("jan@example.com"),
            "hashed_password_123", UserRole.Client);
    }

    // ── RecordFailedLogin escalating lockout ─────────────────

    [Fact]
    public void RecordFailedLogin_FiveFailures_ShouldLockFor10Minutes()
    {
        // Arrange
        var user = CreateUser();

        // Act
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin();

        // Assert
        user.FailedLoginAttempts.Should().Be(5);
        user.IsLockedOut.Should().BeTrue();
        user.LockedUntil.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(10), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordFailedLogin_EightFailures_ShouldLockFor1Hour()
    {
        // Arrange
        var user = CreateUser();

        // Act
        for (var i = 0; i < 8; i++)
            user.RecordFailedLogin();

        // Assert
        user.FailedLoginAttempts.Should().Be(8);
        user.IsLockedOut.Should().BeTrue();
        user.LockedUntil.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordFailedLogin_TwelveFailures_ShouldLockFor24Hours()
    {
        // Arrange
        var user = CreateUser();

        // Act
        for (var i = 0; i < 12; i++)
            user.RecordFailedLogin();

        // Assert
        user.FailedLoginAttempts.Should().Be(12);
        user.IsLockedOut.Should().BeTrue();
        user.LockedUntil.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordFailedLogin_FourFailures_ShouldNotLock()
    {
        // Arrange
        var user = CreateUser();

        // Act
        for (var i = 0; i < 4; i++)
            user.RecordFailedLogin();

        // Assert
        user.FailedLoginAttempts.Should().Be(4);
        user.IsLockedOut.Should().BeFalse();
        user.LockedUntil.Should().BeNull();
    }

    // ── RecordSuccessfulLogin ────────────────────────────────

    [Fact]
    public void RecordSuccessfulLogin_ShouldResetCountersAndSetSession()
    {
        // Arrange
        var user = CreateUser();
        for (var i = 0; i < 3; i++)
            user.RecordFailedLogin();

        var sessionId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddHours(8);

        // Act
        user.RecordSuccessfulLogin(sessionId, expiresAt);

        // Assert
        user.FailedLoginAttempts.Should().Be(0);
        user.LockedUntil.Should().BeNull();
        user.ActiveSessionId.Should().Be(sessionId);
        user.SessionExpiresAt.Should().Be(expiresAt);
    }

    // ── InvalidateSession ────────────────────────────────────

    [Fact]
    public void InvalidateSession_WithMatchingSessionId_ShouldClearSession()
    {
        // Arrange
        var user = CreateUser();
        var sessionId = Guid.NewGuid();
        user.RecordSuccessfulLogin(sessionId, DateTime.UtcNow.AddHours(8));

        // Act
        user.InvalidateSession(sessionId);

        // Assert
        user.ActiveSessionId.Should().BeNull();
    }

    [Fact]
    public void InvalidateSession_WithDifferentSessionId_ShouldNotClearSession()
    {
        // Arrange
        var user = CreateUser();
        var sessionId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();
        user.RecordSuccessfulLogin(sessionId, DateTime.UtcNow.AddHours(8));

        // Act
        user.InvalidateSession(otherSessionId);

        // Assert
        user.ActiveSessionId.Should().Be(sessionId);
    }

    // ── IsSessionValid ───────────────────────────────────────

    [Fact]
    public void IsSessionValid_WithValidSession_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateUser();
        var sessionId = Guid.NewGuid();
        user.RecordSuccessfulLogin(sessionId, DateTime.UtcNow.AddHours(8));

        // Act
        var result = user.IsSessionValid(sessionId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSessionValid_WithExpiredSession_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateUser();
        var sessionId = Guid.NewGuid();
        user.RecordSuccessfulLogin(sessionId, DateTime.UtcNow.AddSeconds(-1));

        // Act
        var result = user.IsSessionValid(sessionId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSessionValid_WithWrongSessionId_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateUser();
        var sessionId = Guid.NewGuid();
        user.RecordSuccessfulLogin(sessionId, DateTime.UtcNow.AddHours(8));

        // Act
        var result = user.IsSessionValid(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    // ── SoftDelete + Restore cycle ───────────────────────────

    [Fact]
    public void SoftDelete_ThenRestore_ShouldReturnToActiveState()
    {
        // Arrange
        var user = CreateUser();

        // Act
        user.SoftDelete();
        user.Restore();

        // Assert
        user.IsActive.Should().BeTrue();
        user.IsDeleted.Should().BeFalse();
        user.DeletedAt.Should().BeNull();
    }
}
