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

    public async Task AppendEventsAsync(Account account, CancellationToken ct = default)
    {
        var events = account.GetUncommittedEvents();
        if (events.Count == 0) return;

        var state = await session.Events.FetchStreamStateAsync(account.Id, ct);

        if (state is null)
        {
            session.Events.StartStream(account.Id, events.ToArray());
        }
        else
        {
            session.Events.Append(account.Id, events.ToArray());
        }

        account.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }
}
