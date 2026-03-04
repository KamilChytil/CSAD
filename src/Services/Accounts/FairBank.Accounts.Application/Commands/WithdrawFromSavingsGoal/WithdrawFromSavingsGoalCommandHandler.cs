using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.WithdrawFromSavingsGoal;

public sealed class WithdrawFromSavingsGoalCommandHandler(ISavingsGoalEventStore savingsGoalEventStore)
    : IRequestHandler<WithdrawFromSavingsGoalCommand>
{
    public async Task Handle(WithdrawFromSavingsGoalCommand request, CancellationToken ct)
    {
        var goal = await savingsGoalEventStore.LoadAsync(request.GoalId, ct)
            ?? throw new InvalidOperationException($"Savings goal {request.GoalId} not found.");

        goal.Withdraw(Money.Create(request.Amount, request.Currency));

        await savingsGoalEventStore.AppendEventsAsync(goal, ct);
    }
}
