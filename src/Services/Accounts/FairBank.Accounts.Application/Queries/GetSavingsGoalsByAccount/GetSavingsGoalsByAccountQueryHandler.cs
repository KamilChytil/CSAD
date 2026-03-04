using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetSavingsGoalsByAccount;

public sealed class GetSavingsGoalsByAccountQueryHandler(ISavingsGoalEventStore savingsGoalEventStore)
    : IRequestHandler<GetSavingsGoalsByAccountQuery, IReadOnlyList<SavingsGoalResponse>>
{
    public async Task<IReadOnlyList<SavingsGoalResponse>> Handle(GetSavingsGoalsByAccountQuery request, CancellationToken ct)
    {
        var goals = await savingsGoalEventStore.LoadByAccountAsync(request.AccountId, ct);

        return goals
            .Select(MapToResponse)
            .ToList();
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
