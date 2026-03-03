using FluentAssertions;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Domain;

public class AccountTests
{
    [Fact]
    public void Create_ShouldInitializeWithZeroBalance()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);

        account.Id.Should().NotBe(Guid.Empty);
        account.Balance.Amount.Should().Be(0);
        account.Balance.Currency.Should().Be(Currency.CZK);
        account.AccountNumber.Should().NotBeNull();
        account.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldRaiseAccountCreatedEvent()
    {
        var ownerId = Guid.NewGuid();
        var account = Account.Create(ownerId, Currency.CZK);

        var events = account.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<AccountCreated>();

        var evt = (AccountCreated)events[0];
        evt.OwnerId.Should().Be(ownerId);
        evt.Currency.Should().Be(Currency.CZK);
    }

    [Fact]
    public void Deposit_ShouldIncreaseBalance()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);

        account.Deposit(Money.Create(500, Currency.CZK), "Initial deposit");

        account.Balance.Amount.Should().Be(500);
    }

    [Fact]
    public void Deposit_ShouldRaiseMoneyDepositedEvent()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);

        account.Deposit(Money.Create(500, Currency.CZK), "Deposit");

        var events = account.GetUncommittedEvents();
        events.Should().HaveCount(2); // AccountCreated + MoneyDeposited
        events[1].Should().BeOfType<MoneyDeposited>();
    }

    [Fact]
    public void Withdraw_WithSufficientFunds_ShouldDecreaseBalance()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.Deposit(Money.Create(1000, Currency.CZK), "Deposit");

        account.Withdraw(Money.Create(300, Currency.CZK), "ATM withdrawal");

        account.Balance.Amount.Should().Be(700);
    }

    [Fact]
    public void Withdraw_WithInsufficientFunds_ShouldThrow()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.Deposit(Money.Create(100, Currency.CZK), "Deposit");

        var act = () => account.Withdraw(Money.Create(500, Currency.CZK), "Too much");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public void Withdraw_FromInactiveAccount_ShouldThrow()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.Deposit(Money.Create(1000, Currency.CZK), "Deposit");
        account.Deactivate();

        var act = () => account.Withdraw(Money.Create(100, Currency.CZK), "Attempt");

        act.Should().Throw<InvalidOperationException>().WithMessage("*not active*");
    }

    [Fact]
    public void SetSpendingLimit_ShouldSetLimitAndRequireApproval()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.ClearUncommittedEvents();

        account.SetSpendingLimit(Money.Create(500, Currency.CZK));

        account.SpendingLimit!.Amount.Should().Be(500);
        account.RequiresApproval.Should().BeTrue();
        account.ApprovalThreshold!.Amount.Should().Be(500);
        account.GetUncommittedEvents().Should().HaveCount(1);
    }

    [Fact]
    public void NeedsApproval_OverThreshold_ShouldReturnTrue()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.SetSpendingLimit(Money.Create(500, Currency.CZK));

        account.NeedsApproval(Money.Create(600, Currency.CZK)).Should().BeTrue();
        account.NeedsApproval(Money.Create(400, Currency.CZK)).Should().BeFalse();
    }
}
