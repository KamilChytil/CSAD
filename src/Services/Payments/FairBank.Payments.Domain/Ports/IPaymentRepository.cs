using FairBank.Payments.Domain.Entities;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Ports;

public interface IPaymentRepository : IRepository<Payment, Guid>
{
    Task<IReadOnlyList<Payment>> GetByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetSentByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetReceivedByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default);
}
