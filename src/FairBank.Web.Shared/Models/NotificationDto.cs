namespace FairBank.Web.Shared.Models;

public sealed record NotificationDto(
    Guid Id, Guid UserId, string Type, string Title, string Message,
    bool IsRead, Guid? RelatedEntityId, string? RelatedEntityType, DateTime CreatedAt);
