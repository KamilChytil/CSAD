using FairBank.Identity.Application.Notifications.Commands.CreateNotification;
using FairBank.Identity.Application.Notifications.Commands.MarkAllRead;
using FairBank.Identity.Application.Notifications.Commands.MarkNotificationRead;
using FairBank.Identity.Application.Notifications.Queries.GetNotifications;
using FairBank.Identity.Application.Notifications.Queries.GetUnreadCount;
using FairBank.Identity.Domain.Enums;
using MediatR;

namespace FairBank.Identity.Api.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications").WithTags("Notifications");

        group.MapGet("/", async (Guid userId, bool? unreadOnly, ISender sender) =>
        {
            var result = await sender.Send(new GetNotificationsQuery(userId, unreadOnly ?? false));
            return Results.Ok(result);
        }).WithName("GetNotifications");

        group.MapGet("/count", async (Guid userId, ISender sender) =>
        {
            var count = await sender.Send(new GetUnreadCountQuery(userId));
            return Results.Ok(new { count });
        }).WithName("GetUnreadCount");

        group.MapPost("/", async (CreateNotificationRequest req, ISender sender) =>
        {
            if (!Enum.TryParse<NotificationType>(req.Type, true, out var type))
                return Results.BadRequest("Invalid notification type.");
            var result = await sender.Send(new CreateNotificationCommand(
                req.UserId, type, req.Title, req.Message, req.RelatedEntityId, req.RelatedEntityType));
            return Results.Created($"/api/v1/notifications/{result.Id}", result);
        }).WithName("CreateNotification");

        group.MapPost("/{id:guid}/read", async (Guid id, ISender sender) =>
        {
            await sender.Send(new MarkNotificationReadCommand(id));
            return Results.Ok();
        }).WithName("MarkNotificationRead");

        group.MapPost("/read-all", async (Guid userId, ISender sender) =>
        {
            await sender.Send(new MarkAllReadCommand(userId));
            return Results.Ok();
        }).WithName("MarkAllNotificationsRead");

        return group;
    }
}

public sealed record CreateNotificationRequest(
    Guid UserId, string Type, string Title, string Message,
    Guid? RelatedEntityId = null, string? RelatedEntityType = null);
