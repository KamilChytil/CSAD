using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Payments.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(PaymentsDbContext context) : IPaymentRepository
{
    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Payment aggregate, CancellationToken ct = default)
        => await context.Payments.AddAsync(aggregate, ct);

    public Task UpdateAsync(Payment aggregate, CancellationToken ct = default)
    {
        context.Payments.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Payment>> GetByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default)
        => await context.Payments
            .Where(p => p.SenderAccountId == accountId || p.RecipientAccountId == accountId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Payment>> GetSentByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default)
        => await context.Payments
            .Where(p => p.SenderAccountId == accountId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Payment>> GetReceivedByAccountIdAsync(Guid accountId, int limit = 50, CancellationToken ct = default)
        => await context.Payments
            .Where(p => p.RecipientAccountId == accountId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
}
