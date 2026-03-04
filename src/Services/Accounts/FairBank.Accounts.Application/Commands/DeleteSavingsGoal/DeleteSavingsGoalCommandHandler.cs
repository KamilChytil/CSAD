using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.DeleteSavingsGoal;

public sealed class DeleteSavingsGoalCommandHandler(ISavingsGoalEventStore savingsGoalEventStore)
    : IRequestHandler<DeleteSavingsGoalCommand>
{
    public async Task Handle(DeleteSavingsGoalCommand request, CancellationToken ct)
    {
        var goal = await savingsGoalEventStore.LoadAsync(request.GoalId, ct)
            ?? throw new InvalidOperationException($"Savings goal {request.GoalId} not found.");

        goal.Delete();

        await savingsGoalEventStore.AppendEventsAsync(goal, ct);
    }
}
