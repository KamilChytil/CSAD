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

    public async Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Guid? userId,
        string? action,
        string? entityName,
        DateTime? startDate,
        DateTime? endDate,
        string sortBy = "Timestamp",
        bool sortDesc = true,
        CancellationToken ct = default)
    {
        var query = db.AuditLogs.AsNoTracking();

        if (userId.HasValue)
            query = query.Where(l => l.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action == action);

        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(l => l.EntityName == entityName);

        if (startDate.HasValue)
            query = query.Where(l => l.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(l => l.Timestamp <= endDate.Value);

        // Apply Sorting
        query = sortBy.ToLowerInvariant() switch
        {
            "action" => sortDesc ? query.OrderByDescending(l => l.Action) : query.OrderBy(l => l.Action),
            "entityname" => sortDesc ? query.OrderByDescending(l => l.EntityName) : query.OrderBy(l => l.EntityName),
            "userid" => sortDesc ? query.OrderByDescending(l => l.UserId) : query.OrderBy(l => l.UserId),
            _ => sortDesc ? query.OrderByDescending(l => l.Timestamp) : query.OrderBy(l => l.Timestamp)
        };

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
