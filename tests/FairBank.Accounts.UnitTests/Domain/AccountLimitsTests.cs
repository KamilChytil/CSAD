using FluentAssertions;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Domain;

public class AccountLimitsTests
{
    [Fact]
    public void Create_WithDefaults_ShouldReturnDefaultLimits()
    {
        var limits = AccountLimits.Create();

        limits.DailyTransactionLimit.Should().Be(100000);
        limits.MonthlyTransactionLimit.Should().Be(500000);
        limits.SingleTransactionLimit.Should().Be(50000);
        limits.DailyTransactionCount.Should().Be(50);
        limits.OnlinePaymentLimit.Should().Be(30000);
    }

    [Fact]
    public void Default_ShouldReturnSameAsCreateWithDefaults()
    {
        var defaults = AccountLimits.Default();
        var created = AccountLimits.Create();

        defaults.Should().Be(created);
    }

    [Fact]
    public void Create_WithCustomValues_ShouldSetAllProperties()
    {
        var limits = AccountLimits.Create(
            dailyLimit: 200000,
            monthlyLimit: 800000,
            singleLimit: 100000,
            dailyCount: 100,
            onlineLimit: 50000);

        limits.DailyTransactionLimit.Should().Be(200000);
        limits.MonthlyTransactionLimit.Should().Be(800000);
        limits.SingleTransactionLimit.Should().Be(100000);
        limits.DailyTransactionCount.Should().Be(100);
        limits.OnlinePaymentLimit.Should().Be(50000);
    }

    [Fact]
    public void Create_WithNegativeDailyLimit_ShouldThrow()
    {
        var act = () => AccountLimits.Create(dailyLimit: -1);

        act.Should().Throw<ArgumentException>().WithMessage("*non-negative*");
    }

    [Fact]
    public void Create_WithNegativeMonthlyLimit_ShouldThrow()
    {
        var act = () => AccountLimits.Create(monthlyLimit: -1);

        act.Should().Throw<ArgumentException>().WithMessage("*non-negative*");
    }

    [Fact]
    public void Create_WithNegativeSingleLimit_ShouldThrow()
    {
        var act = () => AccountLimits.Create(singleLimit: -1);

        act.Should().Throw<ArgumentException>().WithMessage("*non-negative*");
    }

    [Fact]
    public void Create_WithNegativeDailyCount_ShouldThrow()
    {
        var act = () => AccountLimits.Create(dailyCount: -1);

        act.Should().Throw<ArgumentException>().WithMessage("*non-negative*");
    }

    [Fact]
    public void Create_WithNegativeOnlineLimit_ShouldThrow()
    {
        var act = () => AccountLimits.Create(onlineLimit: -1);

        act.Should().Throw<ArgumentException>().WithMessage("*non-negative*");
    }

    [Fact]
    public void Create_WithDailyExceedingMonthly_ShouldThrow()
    {
        var act = () => AccountLimits.Create(dailyLimit: 600000, monthlyLimit: 500000);

        act.Should().Throw<ArgumentException>().WithMessage("*Daily limit cannot exceed monthly limit*");
    }

    [Fact]
    public void Create_WithDailyEqualToMonthly_ShouldSucceed()
    {
        var limits = AccountLimits.Create(dailyLimit: 500000, monthlyLimit: 500000);

        limits.DailyTransactionLimit.Should().Be(500000);
        limits.MonthlyTransactionLimit.Should().Be(500000);
    }

    [Fact]
    public void Create_WithZeroValues_ShouldSucceed()
    {
        var limits = AccountLimits.Create(
            dailyLimit: 0,
            monthlyLimit: 0,
            singleLimit: 0,
            dailyCount: 0,
            onlineLimit: 0);

        limits.DailyTransactionLimit.Should().Be(0);
        limits.MonthlyTransactionLimit.Should().Be(0);
        limits.SingleTransactionLimit.Should().Be(0);
        limits.DailyTransactionCount.Should().Be(0);
        limits.OnlinePaymentLimit.Should().Be(0);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = AccountLimits.Create(10000, 50000, 5000, 10, 3000);
        var b = AccountLimits.Create(10000, 50000, 5000, 10, 3000);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = AccountLimits.Create(10000, 50000, 5000, 10, 3000);
        var b = AccountLimits.Create(20000, 50000, 5000, 10, 3000);

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }
}
