using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using Marten;

namespace FairBank.Accounts.Infrastructure.Persistence;

public sealed class MartenInvestmentEventStore(IDocumentSession session) : IInvestmentEventStore
{
    public async Task<Investment?> LoadAsync(Guid investmentId, CancellationToken ct = default)
    {
        return await session.Events.AggregateStreamAsync<Investment>(investmentId, token: ct);
    }

    public async Task<IReadOnlyList<Investment>> LoadByAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var results = await session.Query<Investment>()
            .Where(i => i.AccountId == accountId)
            .ToListAsync(ct);
        return results;
    }

    public async Task StartStreamAsync(Investment investment, CancellationToken ct = default)
    {
        var events = investment.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.StartStream<Investment>(investment.Id, events.ToArray());
        investment.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }

    public async Task AppendEventsAsync(Investment investment, CancellationToken ct = default)
    {
        var events = investment.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.Append(investment.Id, events.ToArray());
        investment.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }
}
