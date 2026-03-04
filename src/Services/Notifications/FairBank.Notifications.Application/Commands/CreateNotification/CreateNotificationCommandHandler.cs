using FairBank.Notifications.Application.DTOs;
using FairBank.Notifications.Application.Hubs;
using FairBank.Notifications.Domain.Entities;
using FairBank.Notifications.Domain.Ports;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace FairBank.Notifications.Application.Commands.CreateNotification;

public sealed class CreateNotificationCommandHandler(
    INotificationRepository repository,
    IHubContext<NotificationHub> hubContext)
    : IRequestHandler<CreateNotificationCommand, NotificationResponse>
{
    public async Task<NotificationResponse> Handle(CreateNotificationCommand request, CancellationToken ct)
    {
        var notification = Notification.Create(
            request.UserId,
            request.Title,
            request.Message,
            request.Type,
            request.Priority,
            request.Channel,
            request.RelatedEntityType,
            request.RelatedEntityId);

        notification.MarkAsSent();

        await repository.AddAsync(notification, ct);

        var response = new NotificationResponse(
            notification.Id,
            notification.UserId,
            notification.Title,
            notification.Message,
            notification.Type,
            notification.Priority,
            notification.Channel,
            notification.Status,
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.CreatedAt,
            notification.ReadAt,
            notification.SentAt);

        await hubContext.Clients.Group($"user-{notification.UserId}")
            .SendAsync("ReceiveNotification", response, ct);

        return response;
    }
}
