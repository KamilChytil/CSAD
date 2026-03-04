using FairBank.Notifications.Application.Commands.CreateNotification;
using FairBank.Notifications.Application.Commands.DeleteNotification;
using FairBank.Notifications.Application.Commands.MarkAllAsRead;
using FairBank.Notifications.Application.Commands.MarkAsRead;
using FairBank.Notifications.Application.Commands.UpdatePreferences;
using FairBank.Notifications.Application.Queries.GetNotifications;
using FairBank.Notifications.Application.Queries.GetPreferences;
using FairBank.Notifications.Application.Queries.GetUnreadCount;
using FairBank.Notifications.Domain.Enums;
using MediatR;

namespace FairBank.Notifications.Api.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications")
            .WithTags("Notifications");

        // POST /api/v1/notifications/ → CreateNotification
        group.MapPost("/", async (CreateNotificationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/notifications/{result.Id}", result);
        })
        .WithName("CreateNotification")
        .Produces(StatusCodes.Status201Created);

        // GET /api/v1/notifications/?userId=&type=&page=&pageSize= → GetNotifications
        group.MapGet("/", async (Guid userId, NotificationType? type, int? page, int? pageSize, ISender sender) =>
        {
            var result = await sender.Send(new GetNotificationsQuery(userId, type, page ?? 1, pageSize ?? 20));
            return Results.Ok(result);
        })
        .WithName("GetNotifications")
        .Produces(StatusCodes.Status200OK);

        // GET /api/v1/notifications/unread-count?userId= → GetUnreadCount
        group.MapGet("/unread-count", async (Guid userId, ISender sender) =>
        {
            var result = await sender.Send(new GetUnreadCountQuery(userId));
            return Results.Ok(new { UnreadCount = result });
        })
        .WithName("GetUnreadCount")
        .Produces(StatusCodes.Status200OK);

        // PUT /api/v1/notifications/{id}/read → MarkAsRead
        group.MapPut("/{id:guid}/read", async (Guid id, ISender sender) =>
        {
            await sender.Send(new MarkAsReadCommand(id));
            return Results.NoContent();
        })
        .WithName("MarkAsRead")
        .Produces(StatusCodes.Status204NoContent);

        // PUT /api/v1/notifications/read-all?userId= → MarkAllAsRead
        group.MapPut("/read-all", async (Guid userId, ISender sender) =>
        {
            await sender.Send(new MarkAllAsReadCommand(userId));
            return Results.NoContent();
        })
        .WithName("MarkAllAsRead")
        .Produces(StatusCodes.Status204NoContent);

        // DELETE /api/v1/notifications/{id} → DeleteNotification
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteNotificationCommand(id));
            return Results.NoContent();
        })
        .WithName("DeleteNotification")
        .Produces(StatusCodes.Status204NoContent);

        // GET /api/v1/notifications/preferences?userId= → GetPreferences
        group.MapGet("/preferences", async (Guid userId, ISender sender) =>
        {
            var result = await sender.Send(new GetPreferencesQuery(userId));
            return Results.Ok(result);
        })
        .WithName("GetPreferences")
        .Produces(StatusCodes.Status200OK);

        // PUT /api/v1/notifications/preferences → UpdatePreferences
        group.MapPut("/preferences", async (UpdatePreferencesCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .WithName("UpdatePreferences")
        .Produces(StatusCodes.Status200OK);

        return group;
    }
}
