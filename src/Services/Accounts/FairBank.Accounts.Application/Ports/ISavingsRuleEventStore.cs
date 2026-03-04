using FairBank.Accounts.Domain.Aggregates;

namespace FairBank.Accounts.Application.Ports;

public interface ISavingsRuleEventStore
{
    Task<SavingsRule?> LoadAsync(Guid ruleId, CancellationToken ct = default);
    Task<IReadOnlyList<SavingsRule>> LoadByAccountAsync(Guid accountId, CancellationToken ct = default);
    Task StartStreamAsync(SavingsRule rule, CancellationToken ct = default);
    Task AppendEventsAsync(SavingsRule rule, CancellationToken ct = default);
}
