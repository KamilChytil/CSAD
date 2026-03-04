using FairBank.Identity.Application.Notifications.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Commands.CreateNotification;

public sealed class CreateNotificationCommandHandler(
    INotificationRepository notificationRepo, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateNotificationCommand, NotificationResponse>
{
    public async Task<NotificationResponse> Handle(CreateNotificationCommand request, CancellationToken ct)
    {
        var notification = Notification.Create(
            request.UserId, request.Type, request.Title, request.Message,
            request.RelatedEntityId, request.RelatedEntityType);
        await notificationRepo.AddAsync(notification, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new NotificationResponse(
            notification.Id, notification.UserId, notification.Type.ToString(),
            notification.Title, notification.Message, notification.IsRead,
            notification.RelatedEntityId, notification.RelatedEntityType, notification.CreatedAt);
    }
}
