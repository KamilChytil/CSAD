using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Payments.Infrastructure.Persistence.Repositories;

public sealed class ExchangeFavoriteRepository(PaymentsDbContext context) : IExchangeFavoriteRepository
{
    public async Task<ExchangeFavorite?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.ExchangeFavorites.FindAsync([id], ct);

    public async Task AddAsync(ExchangeFavorite aggregate, CancellationToken ct = default)
        => await context.ExchangeFavorites.AddAsync(aggregate, ct);

    public Task UpdateAsync(ExchangeFavorite aggregate, CancellationToken ct = default)
    {
        context.ExchangeFavorites.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ExchangeFavorite>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await context.ExchangeFavorites
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

    public void Remove(ExchangeFavorite entity)
        => context.ExchangeFavorites.Remove(entity);
}
