using FluentAssertions;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;

namespace FairBank.Accounts.UnitTests.Domain;

public class SavingsGoalDeleteTests
{
    [Fact]
    public void Delete_ShouldSetIsDeletedAndDeletedAt()
    {
        var goal = SavingsGoal.Create(Guid.NewGuid(), "Vacation", "Trip to Italy", 5000m, Currency.CZK);
        goal.ClearUncommittedEvents();

        goal.Delete();

        goal.IsDeleted.Should().BeTrue();
        goal.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Delete_ShouldRaiseSavingsGoalDeletedEvent()
    {
        var goal = SavingsGoal.Create(Guid.NewGuid(), "Vacation", null, 5000m, Currency.CZK);
        goal.ClearUncommittedEvents();

        goal.Delete();

        var events = goal.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<SavingsGoalDeleted>();

        var evt = (SavingsGoalDeleted)events[0];
        evt.GoalId.Should().Be(goal.Id);
        evt.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_ShouldThrow()
    {
        var goal = SavingsGoal.Create(Guid.NewGuid(), "Vacation", null, 5000m, Currency.CZK);
        goal.Delete();

        var act = () => goal.Delete();

        act.Should().Throw<InvalidOperationException>().WithMessage("*already deleted*");
    }

    [Fact]
    public void Apply_SavingsGoalDeleted_ShouldSetState()
    {
        var goal = SavingsGoal.Create(Guid.NewGuid(), "Vacation", null, 5000m, Currency.CZK);
        var occurredAt = DateTime.UtcNow;

        var evt = new SavingsGoalDeleted(goal.Id, occurredAt);

        goal.Apply(evt);

        goal.IsDeleted.Should().BeTrue();
        goal.DeletedAt.Should().Be(occurredAt);
    }
}
