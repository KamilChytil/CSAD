using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task AddAsync(User aggregate, CancellationToken ct = default)
    {
        await db.Users.AddAsync(aggregate, ct);
    }

    public Task UpdateAsync(User aggregate, CancellationToken ct = default)
    {
        db.Users.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<User?> GetByEmailAsync(FairBank.Identity.Domain.ValueObjects.Email email, CancellationToken ct = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<bool> ExistsWithEmailAsync(FairBank.Identity.Domain.ValueObjects.Email email, CancellationToken ct = default)
    {
        return await db.Users.AnyAsync(u => u.Email == email, ct);
    }

    public async Task<IReadOnlyList<User>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
    {
        return await db.Users.Where(u => u.ParentId == parentId).ToListAsync(ct);
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Users.ToListAsync(ct);
    }

    public async Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token, ct);
    }

    public async Task<User?> GetByPasswordResetTokenAsync(string token, CancellationToken ct = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token, ct);
    }

    public async Task<User?> GetDeletedByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id && u.IsDeleted, ct);
    }
}
