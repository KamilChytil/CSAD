using MediatR;

namespace FairBank.Identity.Application.Notifications.Queries.GetUnreadCount;

public sealed record GetUnreadCountQuery(Guid UserId) : IRequest<int>;
