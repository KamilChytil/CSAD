using MediatR;

namespace FairBank.Notifications.Application.Commands.DeleteNotification;

public sealed record DeleteNotificationCommand(Guid Id) : IRequest;
