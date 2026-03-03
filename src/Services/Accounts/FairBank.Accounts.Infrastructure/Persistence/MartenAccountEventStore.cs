using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using Marten;

namespace FairBank.Accounts.Infrastructure.Persistence;

public sealed class MartenAccountEventStore(IDocumentSession session) : IAccountEventStore
{
    public async Task<Account?> LoadAsync(Guid accountId, CancellationToken ct = default)
    {
        return await session.Events.AggregateStreamAsync<Account>(accountId, token: ct);
    }

    public async Task<Account?> LoadByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        return await session.Query<Account>().FirstOrDefaultAsync(a => a.AccountNumber.Value == accountNumber, ct);
    }

    public async Task StartStreamAsync(Account account, CancellationToken ct = default)
    {
        var events = account.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.StartStream<Account>(account.Id, events.ToArray());
        account.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }

    public async Task AppendEventsAsync(Account account, CancellationToken ct = default)
    {
        var events = account.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.Append(account.Id, events.ToArray());
        account.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }
}
