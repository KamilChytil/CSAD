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

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<bool> ExistsWithEmailAsync(Email email, CancellationToken ct = default)
    {
        return await db.Users.AnyAsync(u => u.Email.Value == email.Value, ct);
    }
}
