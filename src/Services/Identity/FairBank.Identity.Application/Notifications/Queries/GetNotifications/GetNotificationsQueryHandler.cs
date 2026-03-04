using FairBank.Identity.Application.Notifications.DTOs;
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsQueryHandler(INotificationRepository notificationRepo)
    : IRequestHandler<GetNotificationsQuery, IReadOnlyList<NotificationResponse>>
{
    public async Task<IReadOnlyList<NotificationResponse>> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var notifications = await notificationRepo.GetByUserIdAsync(request.UserId, request.UnreadOnly, ct);
        return notifications.Select(n => new NotificationResponse(
            n.Id, n.UserId, n.Type.ToString(), n.Title, n.Message, n.IsRead,
            n.RelatedEntityId, n.RelatedEntityType, n.CreatedAt)).ToList();
    }
}
