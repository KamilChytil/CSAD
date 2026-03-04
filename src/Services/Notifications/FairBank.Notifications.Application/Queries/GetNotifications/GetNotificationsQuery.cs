using FairBank.Notifications.Application.DTOs;
using FairBank.Notifications.Domain.Enums;
using MediatR;

namespace FairBank.Notifications.Application.Queries.GetNotifications;

public sealed record GetNotificationsQuery(
    Guid UserId,
    NotificationType? Type = null,
    int Page = 1,
    int PageSize = 20) : IRequest<IReadOnlyList<NotificationResponse>>;
