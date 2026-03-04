using FairBank.Cards.Domain.Aggregates;
using FairBank.Cards.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Cards.Infrastructure.Persistence.Repositories;

public sealed class CardRepository(CardsDbContext context) : ICardRepository
{
    public async Task<Card?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Cards.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(Card aggregate, CancellationToken ct = default)
        => await context.Cards.AddAsync(aggregate, ct);

    public Task UpdateAsync(Card aggregate, CancellationToken ct = default)
    {
        context.Cards.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Card>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
        => await context.Cards
            .Where(c => c.AccountId == accountId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Card>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await context.Cards
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
}
