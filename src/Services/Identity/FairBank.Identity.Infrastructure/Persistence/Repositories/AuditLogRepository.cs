using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Identity.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(IdentityDbContext db) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog aggregate, CancellationToken ct = default)
    {
        await db.AuditLogs.AddAsync(aggregate, ct);
    }

    public async Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.AuditLogs.FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public Task UpdateAsync(AuditLog aggregate, CancellationToken ct = default)
    {
        db.AuditLogs.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.AuditLogs.AsNoTracking();
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
