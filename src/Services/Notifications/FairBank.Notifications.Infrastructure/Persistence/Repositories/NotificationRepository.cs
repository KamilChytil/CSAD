using FairBank.Notifications.Domain.Entities;
using FairBank.Notifications.Domain.Enums;
using FairBank.Notifications.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Notifications.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(NotificationsDbContext db) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Notifications.FindAsync([id], ct);

    public async Task<IReadOnlyList<Notification>> GetByUserIdAsync(
        Guid userId, NotificationType? type, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Notifications
            .Where(n => n.UserId == userId);

        if (type.HasValue)
            query = query.Where(n => n.Type == type.Value);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => await db.Notifications
            .CountAsync(n => n.UserId == userId && n.Status != NotificationStatus.Read, ct);

    public async Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        db.Notifications.Update(notification);
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        await db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.Status, NotificationStatus.Read)
                .SetProperty(n => n.ReadAt, now), ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.Notifications
            .Where(n => n.Id == id)
            .ExecuteDeleteAsync(ct);
    }
}
