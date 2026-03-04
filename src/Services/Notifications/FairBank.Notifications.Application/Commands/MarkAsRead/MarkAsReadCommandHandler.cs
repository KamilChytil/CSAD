using FairBank.Notifications.Domain.Ports;
using MediatR;

namespace FairBank.Notifications.Application.Commands.MarkAsRead;

public sealed class MarkAsReadCommandHandler(INotificationRepository repository)
    : IRequestHandler<MarkAsReadCommand>
{
    public async Task Handle(MarkAsReadCommand request, CancellationToken ct)
    {
        var notification = await repository.GetByIdAsync(request.Id, ct)
            ?? throw new InvalidOperationException("Notification not found.");

        notification.MarkAsRead();
        await repository.UpdateAsync(notification, ct);
    }
}
