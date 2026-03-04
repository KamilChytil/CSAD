using FairBank.Notifications.Domain.Ports;
using MediatR;

namespace FairBank.Notifications.Application.Commands.DeleteNotification;

public sealed class DeleteNotificationCommandHandler(INotificationRepository repository)
    : IRequestHandler<DeleteNotificationCommand>
{
    public async Task Handle(DeleteNotificationCommand request, CancellationToken ct)
    {
        await repository.DeleteAsync(request.Id, ct);
    }
}
