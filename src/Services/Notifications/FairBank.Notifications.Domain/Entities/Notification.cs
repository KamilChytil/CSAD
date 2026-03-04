using FairBank.Notifications.Domain.Enums;

namespace FairBank.Notifications.Domain.Entities;

public sealed class Notification
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public NotificationType Type { get; private set; }
    public NotificationPriority Priority { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string? RelatedEntityType { get; private set; }
    public Guid? RelatedEntityId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid userId, string title, string message,
        NotificationType type, NotificationPriority priority,
        NotificationChannel channel = NotificationChannel.InApp,
        string? relatedEntityType = null, Guid? relatedEntityId = null)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            Priority = priority,
            Channel = channel,
            Status = NotificationStatus.Pending,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
    }

    public void MarkAsRead()
    {
        if (ReadAt is null)
        {
            Status = NotificationStatus.Read;
            ReadAt = DateTime.UtcNow;
        }
    }

    public void MarkAsFailed()
    {
        Status = NotificationStatus.Failed;
    }
}
