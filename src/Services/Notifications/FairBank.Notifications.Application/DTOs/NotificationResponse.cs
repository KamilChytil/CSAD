using FairBank.Notifications.Domain.Enums;

namespace FairBank.Notifications.Application.DTOs;

public sealed record NotificationResponse(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    NotificationType Type,
    NotificationPriority Priority,
    NotificationChannel Channel,
    NotificationStatus Status,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    DateTime CreatedAt,
    DateTime? ReadAt,
    DateTime? SentAt);
