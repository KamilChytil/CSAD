using FairBank.Payments.Domain.Entities;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Ports;

public interface IExchangeTransactionRepository : IRepository<ExchangeTransaction, Guid>
{
    Task<IReadOnlyList<ExchangeTransaction>> GetByUserIdAsync(Guid userId, int limit = 20, CancellationToken ct = default);
}
