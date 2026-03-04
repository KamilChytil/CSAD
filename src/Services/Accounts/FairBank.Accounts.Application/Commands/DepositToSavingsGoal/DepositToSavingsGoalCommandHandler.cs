using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.DepositToSavingsGoal;

public sealed class DepositToSavingsGoalCommandHandler(ISavingsGoalEventStore savingsGoalEventStore)
    : IRequestHandler<DepositToSavingsGoalCommand>
{
    public async Task Handle(DepositToSavingsGoalCommand request, CancellationToken ct)
    {
        var goal = await savingsGoalEventStore.LoadAsync(request.GoalId, ct)
            ?? throw new InvalidOperationException($"Savings goal {request.GoalId} not found.");

        goal.Deposit(Money.Create(request.Amount, request.Currency));

        await savingsGoalEventStore.AppendEventsAsync(goal, ct);
    }
}
