using FairBank.Notifications.Application.DTOs;
using FairBank.Notifications.Domain.Enums;
using MediatR;

namespace FairBank.Notifications.Application.Commands.CreateNotification;

public sealed record CreateNotificationCommand(
    Guid UserId,
    string Title,
    string Message,
    NotificationType Type,
    NotificationPriority Priority,
    NotificationChannel Channel = NotificationChannel.InApp,
    string? RelatedEntityType = null,
    Guid? RelatedEntityId = null) : IRequest<NotificationResponse>;
