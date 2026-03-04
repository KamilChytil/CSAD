using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using Marten;

namespace FairBank.Accounts.Infrastructure.Persistence;

public sealed class MartenSavingsRuleEventStore(IDocumentSession session) : ISavingsRuleEventStore
{
    public async Task<SavingsRule?> LoadAsync(Guid ruleId, CancellationToken ct = default)
    {
        return await session.Events.AggregateStreamAsync<SavingsRule>(ruleId, token: ct);
    }

    public async Task<IReadOnlyList<SavingsRule>> LoadByAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var results = await session.Query<SavingsRule>()
            .Where(r => r.AccountId == accountId)
            .ToListAsync(ct);
        return results;
    }

    public async Task StartStreamAsync(SavingsRule rule, CancellationToken ct = default)
    {
        var events = rule.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.StartStream<SavingsRule>(rule.Id, events.ToArray());
        rule.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }

    public async Task AppendEventsAsync(SavingsRule rule, CancellationToken ct = default)
    {
        var events = rule.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.Append(rule.Id, events.ToArray());
        rule.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }
}
