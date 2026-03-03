using FairBank.Payments.Domain.Entities;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Ports;

public interface IPaymentTemplateRepository : IRepository<PaymentTemplate, Guid>
{
    Task<IReadOnlyList<PaymentTemplate>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
}
