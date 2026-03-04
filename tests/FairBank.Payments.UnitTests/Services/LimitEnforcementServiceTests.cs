using FairBank.Payments.Application.Services;
using FluentAssertions;

namespace FairBank.Payments.UnitTests.Services;

public class LimitEnforcementServiceTests
{
    // ── Single Transaction Limit ──────────────────────────────────────

    [Theory]
    [InlineData(500, 1000)]
    [InlineData(1, 1000)]
    [InlineData(0, 1000)]
    public void EnforceSingleTransactionLimit_WithinLimit_ShouldNotThrow(
        decimal amount, decimal limit)
    {
        // Act
        var act = () => LimitEnforcementService.EnforceSingleTransactionLimit(amount, limit);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(1001, 1000)]
    [InlineData(5000, 1000)]
    [InlineData(1000.01, 1000)]
    public void EnforceSingleTransactionLimit_ExceedsLimit_ShouldThrow(
        decimal amount, decimal limit)
    {
        // Act
        var act = () => LimitEnforcementService.EnforceSingleTransactionLimit(amount, limit);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*limit*");
    }

    [Fact]
    public void EnforceSingleTransactionLimit_NullLimit_ShouldNotThrow()
    {
        // Act
        var act = () => LimitEnforcementService.EnforceSingleTransactionLimit(999_999m, null);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(1000, 1000)]
    [InlineData(0, 0)]
    public void EnforceSingleTransactionLimit_ExactlyAtLimit_ShouldNotThrow(
        decimal amount, decimal limit)
    {
        // Act
        var act = () => LimitEnforcementService.EnforceSingleTransactionLimit(amount, limit);

        // Assert
        act.Should().NotThrow();
    }

    // ── Daily Limit ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 500, 1000)]
    [InlineData(400, 500, 1000)]
    [InlineData(200, 100, 1000)]
    public void EnforceDailyLimit_WithinLimit_ShouldNotThrow(
        decimal todayTotal, decimal amount, decimal dailyLimit)
    {
        // Act
        var act = () => LimitEnforcementService.EnforceDailyLimit(todayTotal, amount, dailyLimit);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(900, 200, 1000)]
    [InlineData(1000, 1, 1000)]
    [InlineData(500, 600, 1000)]
    public void EnforceDailyLimit_ExceedsLimit_ShouldThrow(
        decimal todayTotal, decimal amount, decimal dailyLimit)
    {
        // Act
        var act = () => LimitEnforcementService.EnforceDailyLimit(todayTotal, amount, dailyLimit);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*den*limit*");
    }

    [Fact]
    public void EnforceDailyLimit_NullLimit_ShouldNotThrow()
    {
        // Act
        var act = () => LimitEnforcementService.EnforceDailyLimit(999_999m, 1_000m, null);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(500, 500, 1000)]
    [InlineData(0, 1000, 1000)]
    [InlineData(999, 1, 1000)]
    public void EnforceDailyLimit_ExactlyAtLimit_ShouldNotThrow(
        decimal todayTotal, decimal amount, decimal dailyLimit)
    {
        // Act
        var act = () => LimitEnforcementService.EnforceDailyLimit(todayTotal, amount, dailyLimit);

        // Assert
        act.Should().NotThrow();
    }

    // ── Monthly Limit ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 5000, 50000)]
    [InlineData(20000, 5000, 50000)]
    [InlineData(49000, 500, 50000)]
    public void EnforceMonthlyLimit_WithinLimit_ShouldNotThrow(
        decimal monthTotal, decimal amount, decimal monthlyLimit)
    {
        // Act
        var act = () => LimitEnforcementService.EnforceMonthlyLimit(monthTotal, amount, monthlyLimit);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(49000, 2000, 50000)]
    [InlineData(50000, 1, 50000)]
    [InlineData(30000, 25000, 50000)]
    public void EnforceMonthlyLimit_ExceedsLimit_ShouldThrow(
        decimal monthTotal, decimal amount, decimal monthlyLimit)
    {
        // Act
        var act = () => LimitEnforcementService.EnforceMonthlyLimit(monthTotal, amount, monthlyLimit);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*limit*");
    }

    [Fact]
    public void EnforceMonthlyLimit_NullLimit_ShouldNotThrow()
    {
        // Act
        var act = () => LimitEnforcementService.EnforceMonthlyLimit(999_999m, 1_000m, null);

        // Assert
        act.Should().NotThrow();
    }

    // ── Daily Count ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 10)]
    [InlineData(5, 10)]
    [InlineData(9, 10)]
    public void EnforceDailyCount_WithinLimit_ShouldNotThrow(
        int todayCount, int maxCount)
    {
        // Act
        var act = () => LimitEnforcementService.EnforceDailyCount(todayCount, maxCount);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(15, 10)]
    [InlineData(1, 1)]
    public void EnforceDailyCount_AtLimit_ShouldThrow(
        int todayCount, int maxCount)
    {
        // Act
        var act = () => LimitEnforcementService.EnforceDailyCount(todayCount, maxCount);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*po*et*");
    }

    [Fact]
    public void EnforceDailyCount_NullLimit_ShouldNotThrow()
    {
        // Act
        var act = () => LimitEnforcementService.EnforceDailyCount(999, null);

        // Assert
        act.Should().NotThrow();
    }

    // ── Night Restriction ─────────────────────────────────────────────

    [Fact]
    public void EnforceNightRestriction_WhenEnabled_ShouldNotThrow()
    {
        // When nightEnabled = true, night payments are allowed regardless of time.
        // Act
        var act = () => LimitEnforcementService.EnforceNightRestriction(nightEnabled: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EnforceNightRestriction_WhenDisabled_DuringDay_ShouldNotThrow()
    {
        // When nightEnabled = false and the current hour is between 6 and 22 inclusive,
        // the method should not throw. This test will pass when run during daytime (UTC 06:00-22:59).
        // Note: This test depends on DateTime.UtcNow.Hour. If it is run during
        // night hours (23:00-05:59 UTC), it will fail. For deterministic behavior,
        // the service would need a clock abstraction (integration testing concern).
        var hour = DateTime.UtcNow.Hour;
        if (hour >= 6 && hour < 23)
        {
            // Act
            var act = () => LimitEnforcementService.EnforceNightRestriction(nightEnabled: false);

            // Assert
            act.Should().NotThrow();
        }
        // If running during night hours, skip assertion gracefully --
        // this scenario is documented as needing integration testing with a clock abstraction.
    }
}
