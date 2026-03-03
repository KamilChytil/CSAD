using FluentAssertions;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Create_WithValidAmount_ShouldSucceed()
    {
        var money = Money.Create(100.50m, Currency.CZK);
        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be(Currency.CZK);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrow()
    {
        var act = () => Money.Create(-1, Currency.CZK);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_SameCurrency_ShouldReturnSum()
    {
        var a = Money.Create(100, Currency.CZK);
        var b = Money.Create(50, Currency.CZK);
        var result = a.Add(b);
        result.Amount.Should().Be(150);
    }

    [Fact]
    public void Add_DifferentCurrency_ShouldThrow()
    {
        var czk = Money.Create(100, Currency.CZK);
        var eur = Money.Create(50, Currency.EUR);
        var act = () => czk.Add(eur);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Subtract_WithSufficientFunds_ShouldSucceed()
    {
        var a = Money.Create(100, Currency.CZK);
        var b = Money.Create(30, Currency.CZK);
        var result = a.Subtract(b);
        result.Amount.Should().Be(70);
    }

    [Fact]
    public void Subtract_WithInsufficientFunds_ShouldThrow()
    {
        var a = Money.Create(10, Currency.CZK);
        var b = Money.Create(50, Currency.CZK);
        var act = () => a.Subtract(b);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient*");
    }
}
