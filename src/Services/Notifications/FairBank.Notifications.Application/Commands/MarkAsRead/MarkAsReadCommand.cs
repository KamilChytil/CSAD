using MediatR;

namespace FairBank.Notifications.Application.Commands.MarkAsRead;

public sealed record MarkAsReadCommand(Guid Id) : IRequest;
