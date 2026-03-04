using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreateSavingsRule;

public sealed class CreateSavingsRuleCommandHandler(ISavingsRuleEventStore savingsRuleEventStore)
    : IRequestHandler<CreateSavingsRuleCommand, SavingsRuleResponse>
{
    public async Task<SavingsRuleResponse> Handle(CreateSavingsRuleCommand request, CancellationToken ct)
    {
        var rule = SavingsRule.Create(
            request.AccountId,
            request.Name,
            request.Description,
            request.Type,
            request.Amount);

        await savingsRuleEventStore.StartStreamAsync(rule, ct);

        return MapToResponse(rule);
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
