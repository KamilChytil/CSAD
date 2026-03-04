using FairBank.Notifications.Domain.Ports;
using MediatR;

namespace FairBank.Notifications.Application.Queries.GetUnreadCount;

public sealed class GetUnreadCountQueryHandler(INotificationRepository repository)
    : IRequestHandler<GetUnreadCountQuery, int>
{
    public async Task<int> Handle(GetUnreadCountQuery request, CancellationToken ct)
    {
        return await repository.GetUnreadCountAsync(request.UserId, ct);
    }
}
