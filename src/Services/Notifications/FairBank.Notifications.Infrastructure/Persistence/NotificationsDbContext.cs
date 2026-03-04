using FairBank.Notifications.Domain.Entities;
using FairBank.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications_service");

        // ── Notification ────────────────────────────────────────────────────
        var notification = modelBuilder.Entity<Notification>();
        notification.ToTable("notifications");
        notification.HasKey(n => n.Id);
        notification.Property(n => n.UserId).IsRequired();
        notification.Property(n => n.Title).IsRequired().HasMaxLength(200);
        notification.Property(n => n.Message).IsRequired().HasMaxLength(2000);
        notification.Property(n => n.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        notification.Property(n => n.Priority).IsRequired().HasConversion<string>().HasMaxLength(20);
        notification.Property(n => n.Channel).IsRequired().HasConversion<string>().HasMaxLength(20);
        notification.Property(n => n.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        notification.Property(n => n.RelatedEntityType).HasMaxLength(100);
        notification.Property(n => n.RelatedEntityId);
        notification.Property(n => n.CreatedAt).IsRequired();
        notification.Property(n => n.ReadAt);
        notification.Property(n => n.SentAt);
        notification.HasIndex(n => n.UserId);
        notification.HasIndex(n => new { n.UserId, n.Status });

        // ── NotificationPreference ──────────────────────────────────────────
        var preference = modelBuilder.Entity<NotificationPreference>();
        preference.ToTable("notification_preferences");
        preference.HasKey(p => p.Id);
        preference.Property(p => p.UserId).IsRequired();
        preference.Property(p => p.TransactionNotifications).IsRequired();
        preference.Property(p => p.SecurityNotifications).IsRequired();
        preference.Property(p => p.CardNotifications).IsRequired();
        preference.Property(p => p.LimitNotifications).IsRequired();
        preference.Property(p => p.ChatNotifications).IsRequired();
        preference.Property(p => p.EmailNotificationsEnabled).IsRequired();
        preference.Property(p => p.PushNotificationsEnabled).IsRequired();
        preference.HasIndex(p => p.UserId).IsUnique();
    }
}
