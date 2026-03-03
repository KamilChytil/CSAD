using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Payments.Infrastructure.Persistence.Repositories;

public sealed class PaymentTemplateRepository(PaymentsDbContext context) : IPaymentTemplateRepository
{
    public async Task<PaymentTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.PaymentTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(PaymentTemplate aggregate, CancellationToken ct = default)
        => await context.PaymentTemplates.AddAsync(aggregate, ct);

    public Task UpdateAsync(PaymentTemplate aggregate, CancellationToken ct = default)
    {
        context.PaymentTemplates.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PaymentTemplate>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
        => await context.PaymentTemplates
            .Where(t => t.OwnerAccountId == accountId)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
}
