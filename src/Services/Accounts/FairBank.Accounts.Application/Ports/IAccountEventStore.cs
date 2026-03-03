using FairBank.Accounts.Domain.Aggregates;

namespace FairBank.Accounts.Application.Ports;

public interface IAccountEventStore
{
    Task<Account?> LoadAsync(Guid accountId, CancellationToken ct = default);
    Task<Account?> LoadByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task StartStreamAsync(Account account, CancellationToken ct = default);
    Task AppendEventsAsync(Account account, CancellationToken ct = default);
}
