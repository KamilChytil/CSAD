using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetSavingsRulesByAccount;

public sealed class GetSavingsRulesByAccountQueryHandler(ISavingsRuleEventStore savingsRuleEventStore)
    : IRequestHandler<GetSavingsRulesByAccountQuery, IReadOnlyList<SavingsRuleResponse>>
{
    public async Task<IReadOnlyList<SavingsRuleResponse>> Handle(GetSavingsRulesByAccountQuery request, CancellationToken ct)
    {
        var rules = await savingsRuleEventStore.LoadByAccountAsync(request.AccountId, ct);

        return rules
            .Select(MapToResponse)
            .ToList();
    }

    private static SavingsRuleResponse MapToResponse(SavingsRule rule) => new(
        rule.Id,
        rule.AccountId,
        rule.Name,
        rule.Description,
        rule.Type,
        rule.Amount,
        rule.IsEnabled,
        rule.CreatedAt);
}
