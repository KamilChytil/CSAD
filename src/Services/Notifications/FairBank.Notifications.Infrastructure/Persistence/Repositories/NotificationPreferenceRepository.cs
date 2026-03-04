using FairBank.Notifications.Domain.Entities;
using FairBank.Notifications.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Notifications.Infrastructure.Persistence.Repositories;

public sealed class NotificationPreferenceRepository(NotificationsDbContext db) : INotificationPreferenceRepository
{
    public async Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task AddAsync(NotificationPreference preference, CancellationToken ct = default)
    {
        db.NotificationPreferences.Add(preference);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(NotificationPreference preference, CancellationToken ct = default)
    {
        db.NotificationPreferences.Update(preference);
        await db.SaveChangesAsync(ct);
    }
}
