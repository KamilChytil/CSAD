using FairBank.Cards.Application.Commands.BlockCard;
using FairBank.Cards.Application.Commands.CancelCard;
using FairBank.Cards.Application.Commands.DeactivateAllCardsByUser;
using FairBank.Cards.Application.Commands.IssueCard;
using FairBank.Cards.Application.Commands.RenewCard;
using FairBank.Cards.Application.Commands.SetCardLimits;
using FairBank.Cards.Application.Commands.SetCardSettings;
using FairBank.Cards.Application.Commands.SetPin;
using FairBank.Cards.Application.Commands.UnblockCard;
using FairBank.Cards.Application.DTOs;
using FairBank.Cards.Application.Queries.GetCardById;
using FairBank.Cards.Application.Queries.GetCardsByAccount;
using FairBank.Cards.Application.Queries.GetCardsByUser;
using FairBank.SharedKernel.Security;
using MediatR;

namespace FairBank.Cards.Api.Endpoints;

public static class CardEndpoints
{
    public static void MapCardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/cards").WithTags("Cards");

        group.MapPost("/", async (IssueCardCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/cards/{result.Id}", result);
        })
        .WithName("IssueCard")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .RequireAuth();

        group.MapGet("/account/{accountId:guid}", async (Guid accountId, ISender sender) =>
        {
            var result = await sender.Send(new GetCardsByAccountQuery(accountId));
            return Results.Ok(result ?? Array.Empty<CardResponse>());
        })
        .WithName("GetCardsByAccount")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        group.MapGet("/user/{userId:guid}", async (Guid userId, HttpContext httpContext, ISender sender) =>
        {
            var authUserId = httpContext.GetUserId();
            var role = httpContext.GetUserRole();
            if (role != "Admin" && role != "Banker" && authUserId != userId)
                return Results.Json(new { error = "Forbidden" }, statusCode: 403);

            var result = await sender.Send(new GetCardsByUserQuery(userId));
            return Results.Ok(result ?? Array.Empty<CardResponse>());
        })
        .WithName("GetCardsByUser")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCardByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetCardById")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuth();

        group.MapPut("/{id:guid}/block", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new BlockCardCommand(id));
            return Results.Ok(result);
        })
        .WithName("BlockCard")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        group.MapPut("/{id:guid}/unblock", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new UnblockCardCommand(id));
            return Results.Ok(result);
        })
        .WithName("UnblockCard")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new CancelCardCommand(id));
            return Results.Ok(result);
        })
        .WithName("CancelCard")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        group.MapPut("/{id:guid}/limits", async (Guid id, SetCardLimitsCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { CardId = id });
            return Results.Ok(result);
        })
        .WithName("SetCardLimits")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .RequireAuth();

        group.MapPut("/{id:guid}/settings", async (Guid id, SetCardSettingsCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { CardId = id });
            return Results.Ok(result);
        })
        .WithName("SetCardSettings")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        group.MapPost("/{id:guid}/renew", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new RenewCardCommand(id));
            return Results.Created($"/api/v1/cards/{result.Id}", result);
        })
        .WithName("RenewCard")
        .Produces(StatusCodes.Status201Created)
        .RequireAuth();

        group.MapPut("/{id:guid}/pin", async (Guid id, SetPinCommand command, ISender sender) =>
        {
            await sender.Send(command with { CardId = id });
            return Results.NoContent();
        })
        .WithName("SetPin")
        .Produces(StatusCodes.Status204NoContent)
        .RequireAuth();

        // ── Internal / Admin: block all cards for a given user (soft delete cascade) ──
        group.MapPost("/user/{userId:guid}/deactivate-all", async (Guid userId, ISender sender) =>
        {
            var blocked = await sender.Send(new DeactivateAllCardsByUserCommand(userId));
            return Results.Ok(new { blockedCount = blocked });
        })
        .WithName("DeactivateAllCardsByUser")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth()
        .RequireRole("Admin");
    }
}
