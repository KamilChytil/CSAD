using FairBank.Notifications.Application.DTOs;
using FairBank.Notifications.Domain.Ports;
using MediatR;

namespace FairBank.Notifications.Application.Queries.GetNotifications;

public sealed class GetNotificationsQueryHandler(INotificationRepository repository)
    : IRequestHandler<GetNotificationsQuery, IReadOnlyList<NotificationResponse>>
{
    public async Task<IReadOnlyList<NotificationResponse>> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var notifications = await repository.GetByUserIdAsync(
            request.UserId, request.Type, request.Page, request.PageSize, ct);

        return notifications
            .Select(n => new NotificationResponse(
                n.Id,
                n.UserId,
                n.Title,
                n.Message,
                n.Type,
                n.Priority,
                n.Channel,
                n.Status,
                n.RelatedEntityType,
                n.RelatedEntityId,
                n.CreatedAt,
                n.ReadAt,
                n.SentAt))
            .ToList();
    }
}
