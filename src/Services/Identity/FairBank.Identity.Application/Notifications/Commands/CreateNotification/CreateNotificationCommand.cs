using FairBank.Identity.Application.Notifications.DTOs;
using FairBank.Identity.Domain.Enums;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Commands.CreateNotification;

public sealed record CreateNotificationCommand(
    Guid UserId, NotificationType Type, string Title, string Message,
    Guid? RelatedEntityId = null, string? RelatedEntityType = null) : IRequest<NotificationResponse>;
