using MediatR;

namespace FairBank.Identity.Application.Notifications.Commands.MarkAllRead;

public sealed record MarkAllReadCommand(Guid UserId) : IRequest;
