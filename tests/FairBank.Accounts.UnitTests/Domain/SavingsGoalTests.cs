using FluentAssertions;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Domain;

public class SavingsGoalTests
{
    [Fact]
    public void Create_ShouldInitialize()
    {
        var accountId = Guid.NewGuid();
        var goal = SavingsGoal.Create(accountId, "Vacation", "Trip to Italy", 5000m, Currency.CZK);

        goal.Id.Should().NotBe(Guid.Empty);
        goal.AccountId.Should().Be(accountId);
        goal.Name.Should().Be("Vacation");
        goal.Description.Should().Be("Trip to Italy");
        goal.TargetAmount.Amount.Should().Be(5000m);
        goal.TargetAmount.Currency.Should().Be(Currency.CZK);
        goal.CurrentAmount.Amount.Should().Be(0m);
        goal.IsCompleted.Should().BeFalse();
        goal.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldRaiseEvent()
    {
        var accountId = Guid.NewGuid();
        var goal = SavingsGoal.Create(accountId, "New Car", null, 10000m, Currency.EUR);

        var events = goal.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<SavingsGoalCreated>();

        var evt = (SavingsGoalCreated)events[0];
        evt.GoalId.Should().Be(goal.Id);
        evt.AccountId.Should().Be(accountId);
        evt.Name.Should().Be("New Car");
        evt.Description.Should().BeNull();
        evt.TargetAmount.Should().Be(10000m);
        evt.Currency.Should().Be(Currency.EUR);
    }

    [Fact]
    public void Deposit_ShouldIncreaseCurrentAmount()
    {
        var goal = SavingsGoal.Create(Guid.NewGuid(), "Vacation", null, 5000m, Currency.CZK);
        goal.ClearUncommittedEvents();

        goal.Deposit(Money.Create(1000m, Currency.CZK));

        goal.CurrentAmount.Amount.Should().Be(1000m);
        goal.IsCompleted.Should().BeFalse();
        goal.GetUncommittedEvents().Should().HaveCount(1);
        goal.GetUncommittedEvents()[0].Should().BeOfType<SavingsDeposited>();
    }

    [Fact]
    public void Deposit_ShouldAutoCompleteWhenTargetReached()
    {
        var goal = SavingsGoal.Create(Guid.NewGuid(), "Vacation", null, 1000m, Currency.CZK);
        goal.ClearUncommittedEvents();

        goal.Deposit(Money.Create(1000m, Currency.CZK));

        goal.IsCompleted.Should().BeTrue();
        goal.CompletedAt.Should().NotBeNull();
        goal.CurrentAmount.Amount.Should().Be(1000m);

        var events = goal.GetUncommittedEvents();
        events.Should().HaveCount(2);
        events[0].Should().BeOfType<SavingsDeposited>();
        events[1].Should().BeOfType<SavingsGoalCompleted>();
    }

    [Fact]
    public void Withdraw_ShouldDecreaseAmount()
    {
        var goal = SavingsGoal.Create(Guid.NewGuid(), "Vacation", null, 5000m, Currency.CZK);
        goal.ClearUncommittedEvents();

        goal.Deposit(Money.Create(3000m, Currency.CZK));
        goal.ClearUncommittedEvents();

        goal.Withdraw(Money.Create(1000m, Currency.CZK));

        goal.CurrentAmount.Amount.Should().Be(2000m);
        goal.GetUncommittedEvents().Should().HaveCount(1);
        goal.GetUncommittedEvents()[0].Should().BeOfType<SavingsWithdrawn>();
    }

    [Fact]
    public void Withdraw_WithInsufficientFunds_ShouldThrow()
    {
        var goal = SavingsGoal.Create(Guid.NewGuid(), "Vacation", null, 5000m, Currency.CZK);
        goal.ClearUncommittedEvents();

        goal.Deposit(Money.Create(500m, Currency.CZK));

        var act = () => goal.Withdraw(Money.Create(1000m, Currency.CZK));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public void ProgressPercent_ShouldCalculateCorrectly()
    {
        var goal = SavingsGoal.Create(Guid.NewGuid(), "Vacation", null, 2000m, Currency.CZK);

        goal.ProgressPercent.Should().Be(0);

        goal.Deposit(Money.Create(500m, Currency.CZK));
        goal.ProgressPercent.Should().Be(25);

        goal.Deposit(Money.Create(500m, Currency.CZK));
        goal.ProgressPercent.Should().Be(50);

        goal.Deposit(Money.Create(1000m, Currency.CZK));
        goal.ProgressPercent.Should().Be(100);
    }
}
