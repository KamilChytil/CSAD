using FairBank.Accounts.Domain.Aggregates;

namespace FairBank.Accounts.Application.Ports;

public interface ICardEventStore
{
    Task<Card?> LoadAsync(Guid cardId, CancellationToken ct = default);
    Task<IReadOnlyList<Card>> LoadByAccountAsync(Guid accountId, CancellationToken ct = default);
    Task StartStreamAsync(Card card, CancellationToken ct = default);
    Task AppendEventsAsync(Card card, CancellationToken ct = default);
}
