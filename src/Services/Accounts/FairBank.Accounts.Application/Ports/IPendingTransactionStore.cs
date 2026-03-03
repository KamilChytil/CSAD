using FairBank.Accounts.Domain.Aggregates;

namespace FairBank.Accounts.Application.Ports;

public interface IPendingTransactionStore
{
    Task<PendingTransaction?> LoadAsync(Guid transactionId, CancellationToken ct = default);
    Task<IReadOnlyList<PendingTransaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    Task StartStreamAsync(PendingTransaction transaction, CancellationToken ct = default);
    Task AppendEventsAsync(PendingTransaction transaction, CancellationToken ct = default);
}
