using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreateSavingsGoal;

public sealed class CreateSavingsGoalCommandHandler(ISavingsGoalEventStore savingsGoalEventStore)
    : IRequestHandler<CreateSavingsGoalCommand, SavingsGoalResponse>
{
    public async Task<SavingsGoalResponse> Handle(CreateSavingsGoalCommand request, CancellationToken ct)
    {
        var goal = SavingsGoal.Create(
            request.AccountId,
            request.Name,
            request.Description,
            request.TargetAmount,
            request.Currency);

        await savingsGoalEventStore.StartStreamAsync(goal, ct);

        return MapToResponse(goal);
    }

    private static SavingsGoalResponse MapToResponse(SavingsGoal goal) => new(
        goal.Id,
        goal.AccountId,
        goal.Name,
        goal.Description,
        goal.TargetAmount.Amount,
        goal.CurrentAmount.Amount,
        goal.ProgressPercent,
        goal.TargetAmount.Currency.ToString(),
        goal.IsCompleted,
        goal.CreatedAt,
        goal.CompletedAt);
}
