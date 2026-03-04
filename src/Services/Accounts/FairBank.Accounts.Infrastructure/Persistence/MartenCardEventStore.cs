using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using Marten;

namespace FairBank.Accounts.Infrastructure.Persistence;

public sealed class MartenCardEventStore(IDocumentSession session) : ICardEventStore
{
    public async Task<Card?> LoadAsync(Guid cardId, CancellationToken ct = default)
    {
        return await session.Events.AggregateStreamAsync<Card>(cardId, token: ct);
    }

    public async Task<IReadOnlyList<Card>> LoadByAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var results = await session.Query<Card>()
            .Where(c => c.AccountId == accountId && c.IsActive)
            .ToListAsync(ct);
        return results;
    }

    public async Task StartStreamAsync(Card card, CancellationToken ct = default)
    {
        var events = card.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.StartStream<Card>(card.Id, events.ToArray());
        card.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }

    public async Task AppendEventsAsync(Card card, CancellationToken ct = default)
    {
        var events = card.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.Append(card.Id, events.ToArray());
        card.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }
}
