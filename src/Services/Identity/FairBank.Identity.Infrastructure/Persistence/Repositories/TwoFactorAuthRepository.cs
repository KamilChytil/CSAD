using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Identity.Infrastructure.Persistence.Repositories;

public sealed class TwoFactorAuthRepository(IdentityDbContext db) : ITwoFactorAuthRepository
{
    public async Task<TwoFactorAuth?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.TwoFactorAuths.FirstOrDefaultAsync(t => t.UserId == userId, ct);

    public async Task AddAsync(TwoFactorAuth twoFactorAuth, CancellationToken ct = default)
        => await db.TwoFactorAuths.AddAsync(twoFactorAuth, ct);

    public Task UpdateAsync(TwoFactorAuth twoFactorAuth, CancellationToken ct = default)
    {
        db.TwoFactorAuths.Update(twoFactorAuth);
        return Task.CompletedTask;
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        await db.TwoFactorAuths.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
    }
}
