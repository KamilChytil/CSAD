using FluentAssertions;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;

namespace FairBank.Accounts.UnitTests.Domain;

public class InvestmentTests
{
    [Fact]
    public void Create_ShouldInitialize()
    {
        var accountId = Guid.NewGuid();
        var investment = Investment.Create(accountId, "Tesla", InvestmentType.Stock, 10_000m, 5m, 2_000m, Currency.CZK);

        investment.Id.Should().NotBe(Guid.Empty);
        investment.AccountId.Should().Be(accountId);
        investment.Name.Should().Be("Tesla");
        investment.Type.Should().Be(InvestmentType.Stock);
        investment.InvestedAmount.Amount.Should().Be(10_000m);
        investment.InvestedAmount.Currency.Should().Be(Currency.CZK);
        investment.Units.Should().Be(5m);
        investment.PricePerUnit.Should().Be(2_000m);
        investment.IsActive.Should().BeTrue();
        investment.SoldAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldCalculateCurrentValue()
    {
        var investment = Investment.Create(Guid.NewGuid(), "Bond Fund", InvestmentType.Bond, 15_000m, 15m, 1_000m, Currency.EUR);

        // CurrentValue = Units * PricePerUnit = 15 * 1000 = 15000
        investment.CurrentValue.Amount.Should().Be(15_000m);
        investment.CurrentValue.Currency.Should().Be(Currency.EUR);

        var events = investment.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<InvestmentCreated>();

        var evt = (InvestmentCreated)events[0];
        evt.InvestmentId.Should().Be(investment.Id);
        evt.InvestedAmount.Should().Be(15_000m);
        evt.Units.Should().Be(15m);
        evt.PricePerUnit.Should().Be(1_000m);
    }

    [Fact]
    public void UpdateValue_ShouldRecalculateCurrentValue()
    {
        var investment = Investment.Create(Guid.NewGuid(), "Crypto", InvestmentType.Crypto, 5_000m, 0.05m, 100_000m, Currency.CZK);
        investment.ClearUncommittedEvents();

        investment.UpdateValue(120_000m);

        // CurrentValue = 0.05 * 120000 = 6000
        investment.CurrentValue.Amount.Should().Be(6_000m);
        investment.PricePerUnit.Should().Be(120_000m);

        var events = investment.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<InvestmentValueUpdated>();

        var evt = (InvestmentValueUpdated)events[0];
        evt.NewPricePerUnit.Should().Be(120_000m);
    }

    [Fact]
    public void Sell_ShouldDeactivate()
    {
        var investment = Investment.Create(Guid.NewGuid(), "Fund", InvestmentType.Fund, 25_000m, 10m, 2_500m, Currency.CZK);
        investment.ClearUncommittedEvents();

        investment.Sell();

        investment.IsActive.Should().BeFalse();
        investment.SoldAt.Should().NotBeNull();

        var events = investment.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<InvestmentSold>();

        var evt = (InvestmentSold)events[0];
        evt.SoldAmount.Should().Be(25_000m); // CurrentValue at time of sale
        evt.Currency.Should().Be(Currency.CZK);
    }

    [Fact]
    public void Sell_WhenAlreadySold_ShouldThrow()
    {
        var investment = Investment.Create(Guid.NewGuid(), "Fund", InvestmentType.Fund, 25_000m, 10m, 2_500m, Currency.CZK);
        investment.Sell();

        var act = () => investment.Sell();

        act.Should().Throw<InvalidOperationException>().WithMessage("*already sold*");
    }

    [Fact]
    public void ChangePercent_ShouldCalculateCorrectly()
    {
        // Invested 10000, CurrentValue = 5 * 2000 = 10000 → 0%
        var investment = Investment.Create(Guid.NewGuid(), "Stock", InvestmentType.Stock, 10_000m, 5m, 2_000m, Currency.CZK);
        investment.ChangePercent.Should().Be(0m);

        // Price goes up: CurrentValue = 5 * 3000 = 15000 → +50%
        investment.UpdateValue(3_000m);
        investment.ChangePercent.Should().Be(50m);

        // Price goes down: CurrentValue = 5 * 1000 = 5000 → -50%
        investment.UpdateValue(1_000m);
        investment.ChangePercent.Should().Be(-50m);
    }
}
