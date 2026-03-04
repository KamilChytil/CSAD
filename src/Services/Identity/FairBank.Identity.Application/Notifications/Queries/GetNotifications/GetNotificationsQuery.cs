using FairBank.Identity.Application.Notifications.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery(Guid UserId, bool UnreadOnly = false)
    : IRequest<IReadOnlyList<NotificationResponse>>;
