using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using Marten;

namespace FairBank.Accounts.Infrastructure.Persistence;

public sealed class MartenPendingTransactionStore(IDocumentSession session) : IPendingTransactionStore
{
    public async Task<PendingTransaction?> LoadAsync(Guid transactionId, CancellationToken ct = default)
    {
        return await session.Events.AggregateStreamAsync<PendingTransaction>(transactionId, token: ct);
    }

    public async Task<IReadOnlyList<PendingTransaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        return await session.Query<PendingTransaction>()
            .Where(t => t.AccountId == accountId && t.Status == PendingTransactionStatus.Pending)
            .ToListAsync(ct);
    }

    public async Task StartStreamAsync(PendingTransaction transaction, CancellationToken ct = default)
    {
        var events = transaction.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.StartStream<PendingTransaction>(transaction.Id, events.ToArray());
        transaction.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }

    public async Task AppendEventsAsync(PendingTransaction transaction, CancellationToken ct = default)
    {
        var events = transaction.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.Append(transaction.Id, events.ToArray());
        transaction.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }
}
