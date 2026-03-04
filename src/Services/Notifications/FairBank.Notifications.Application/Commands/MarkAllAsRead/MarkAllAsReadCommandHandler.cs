using FairBank.Notifications.Domain.Ports;
using MediatR;

namespace FairBank.Notifications.Application.Commands.MarkAllAsRead;

public sealed class MarkAllAsReadCommandHandler(INotificationRepository repository)
    : IRequestHandler<MarkAllAsReadCommand>
{
    public async Task Handle(MarkAllAsReadCommand request, CancellationToken ct)
    {
        await repository.MarkAllAsReadAsync(request.UserId, ct);
    }
}
