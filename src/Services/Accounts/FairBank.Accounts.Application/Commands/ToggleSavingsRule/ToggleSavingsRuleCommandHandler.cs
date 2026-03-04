using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.ToggleSavingsRule;

public sealed class ToggleSavingsRuleCommandHandler(ISavingsRuleEventStore savingsRuleEventStore)
    : IRequestHandler<ToggleSavingsRuleCommand>
{
    public async Task Handle(ToggleSavingsRuleCommand request, CancellationToken ct)
    {
        var rule = await savingsRuleEventStore.LoadAsync(request.RuleId, ct)
            ?? throw new InvalidOperationException($"Savings rule {request.RuleId} not found.");

        rule.Toggle();

        await savingsRuleEventStore.AppendEventsAsync(rule, ct);
    }
}
