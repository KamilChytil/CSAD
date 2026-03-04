using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Identity.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(IdentityDbContext context) : INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false, CancellationToken ct = default)
    {
        var query = context.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        return await query.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => await context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
        => await context.Notifications.AddAsync(notification, ct);

    public async Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        context.Notifications.Update(notification);
        await Task.CompletedTask;
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
        => await context.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
}
