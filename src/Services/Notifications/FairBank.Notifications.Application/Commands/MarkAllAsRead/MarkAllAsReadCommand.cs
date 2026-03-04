using MediatR;

namespace FairBank.Notifications.Application.Commands.MarkAllAsRead;

public sealed record MarkAllAsReadCommand(Guid UserId) : IRequest;
