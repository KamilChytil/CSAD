namespace FairBank.Identity.Application.Notifications.DTOs;

public sealed record NotificationResponse(
    Guid Id, Guid UserId, string Type, string Title, string Message,
    bool IsRead, Guid? RelatedEntityId, string? RelatedEntityType, DateTime CreatedAt);
