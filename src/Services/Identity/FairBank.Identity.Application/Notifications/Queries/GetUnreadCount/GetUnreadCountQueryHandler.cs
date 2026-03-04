using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Queries.GetUnreadCount;

public sealed class GetUnreadCountQueryHandler(INotificationRepository notificationRepo)
    : IRequestHandler<GetUnreadCountQuery, int>
{
    public async Task<int> Handle(GetUnreadCountQuery request, CancellationToken ct)
        => await notificationRepo.GetUnreadCountAsync(request.UserId, ct);
}
