using FairBank.Payments.Domain.Entities;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Ports;

public interface IStandingOrderRepository : IRepository<StandingOrder, Guid>
{
    Task<IReadOnlyList<StandingOrder>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<StandingOrder>> GetDueOrdersAsync(DateTime currentDate, CancellationToken ct = default);
}
