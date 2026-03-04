using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Commands.DeleteSavingsGoal;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.UnitTests.Application;

public class DeleteSavingsGoalCommandHandlerTests
{
    private readonly ISavingsGoalEventStore _eventStore = Substitute.For<ISavingsGoalEventStore>();

    [Fact]
    public async Task Handle_WithValidGoal_ShouldDeleteAndPersistEvents()
    {
        var goal = SavingsGoal.Create(Guid.NewGuid(), "Vacation", null, 5000m, Currency.CZK);
        _eventStore.LoadAsync(goal.Id, Arg.Any<CancellationToken>()).Returns(goal);

        var handler = new DeleteSavingsGoalCommandHandler(_eventStore);
        var command = new DeleteSavingsGoalCommand(goal.Id);

        await handler.Handle(command, CancellationToken.None);

        goal.IsDeleted.Should().BeTrue();
        await _eventStore.Received(1).AppendEventsAsync(goal, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GoalNotFound_ShouldThrow()
    {
        _eventStore.LoadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SavingsGoal?)null);

        var handler = new DeleteSavingsGoalCommandHandler(_eventStore);
        var command = new DeleteSavingsGoalCommand(Guid.NewGuid());

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
