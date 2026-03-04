using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandHandler(
    INotificationRepository notificationRepo, IUnitOfWork unitOfWork) : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await notificationRepo.GetByIdAsync(request.NotificationId, ct)
            ?? throw new InvalidOperationException("Notification not found.");
        notification.MarkAsRead();
        await notificationRepo.UpdateAsync(notification, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
