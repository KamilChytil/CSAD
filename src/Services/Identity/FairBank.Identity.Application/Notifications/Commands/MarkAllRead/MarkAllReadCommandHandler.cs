using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Commands.MarkAllRead;

public sealed class MarkAllReadCommandHandler(INotificationRepository notificationRepo)
    : IRequestHandler<MarkAllReadCommand>
{
    public async Task Handle(MarkAllReadCommand request, CancellationToken ct)
        => await notificationRepo.MarkAllReadAsync(request.UserId, ct);
}
