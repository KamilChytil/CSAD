using FluentAssertions;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;

namespace FairBank.Accounts.UnitTests.Domain;

public class SavingsRuleTests
{
    [Fact]
    public void Create_ShouldInitializeEnabled()
    {
        var accountId = Guid.NewGuid();
        var rule = SavingsRule.Create(accountId, "Round-up", "Round up every purchase", SavingsRuleType.RoundUp, 10m);

        rule.Id.Should().NotBe(Guid.Empty);
        rule.AccountId.Should().Be(accountId);
        rule.Name.Should().Be("Round-up");
        rule.Description.Should().Be("Round up every purchase");
        rule.Type.Should().Be(SavingsRuleType.RoundUp);
        rule.Amount.Should().Be(10m);
        rule.IsEnabled.Should().BeTrue();
        rule.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldRaiseEvent()
    {
        var accountId = Guid.NewGuid();
        var rule = SavingsRule.Create(accountId, "Weekly Save", null, SavingsRuleType.FixedWeekly, 100m);

        var events = rule.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<SavingsRuleCreated>();

        var evt = (SavingsRuleCreated)events[0];
        evt.RuleId.Should().Be(rule.Id);
        evt.AccountId.Should().Be(accountId);
        evt.Name.Should().Be("Weekly Save");
        evt.Description.Should().BeNull();
        evt.Type.Should().Be(SavingsRuleType.FixedWeekly);
        evt.Amount.Should().Be(100m);
    }

    [Fact]
    public void Toggle_ShouldFlipIsEnabled()
    {
        var rule = SavingsRule.Create(Guid.NewGuid(), "Monthly Save", null, SavingsRuleType.FixedMonthly, 500m);
        rule.ClearUncommittedEvents();

        rule.IsEnabled.Should().BeTrue();

        rule.Toggle();

        rule.IsEnabled.Should().BeFalse();
        var events = rule.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<SavingsRuleToggled>();

        var evt = (SavingsRuleToggled)events[0];
        evt.RuleId.Should().Be(rule.Id);
        evt.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Toggle_Twice_ShouldRevertToOriginal()
    {
        var rule = SavingsRule.Create(Guid.NewGuid(), "Income %", null, SavingsRuleType.PercentageOfIncome, 5m);
        rule.ClearUncommittedEvents();

        rule.Toggle();
        rule.Toggle();

        rule.IsEnabled.Should().BeTrue();
        var events = rule.GetUncommittedEvents();
        events.Should().HaveCount(2);
    }
}
