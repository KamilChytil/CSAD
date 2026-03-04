using MediatR;

namespace FairBank.Notifications.Application.Queries.GetUnreadCount;

public sealed record GetUnreadCountQuery(Guid UserId) : IRequest<int>;
