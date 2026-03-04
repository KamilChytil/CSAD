using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Payments.Infrastructure.Persistence.Repositories;

public sealed class ExchangeTransactionRepository(PaymentsDbContext context) : IExchangeTransactionRepository
{
    public async Task<ExchangeTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.ExchangeTransactions.FindAsync([id], ct);

    public async Task AddAsync(ExchangeTransaction aggregate, CancellationToken ct = default)
        => await context.ExchangeTransactions.AddAsync(aggregate, ct);

    public Task UpdateAsync(ExchangeTransaction aggregate, CancellationToken ct = default)
    {
        context.ExchangeTransactions.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ExchangeTransaction>> GetByUserIdAsync(Guid userId, int limit = 20, CancellationToken ct = default)
        => await context.ExchangeTransactions
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
}
