using FairBank.Accounts.Domain.Aggregates;

namespace FairBank.Accounts.Application.Ports;

public interface IInvestmentEventStore
{
    Task<Investment?> LoadAsync(Guid investmentId, CancellationToken ct = default);
    Task<IReadOnlyList<Investment>> LoadByAccountAsync(Guid accountId, CancellationToken ct = default);
    Task StartStreamAsync(Investment investment, CancellationToken ct = default);
    Task AppendEventsAsync(Investment investment, CancellationToken ct = default);
}
