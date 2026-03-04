using FairBank.Accounts.Application.Commands.DeactivateCard;
using FairBank.Accounts.Application.Commands.FreezeCard;
using FairBank.Accounts.Application.Commands.IssueCard;
using FairBank.Accounts.Application.Commands.SetCardLimits;
using FairBank.Accounts.Application.Commands.UnfreezeCard;
using FairBank.Accounts.Application.Commands.UpdateCardSettings;
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Queries.GetCardsByAccount;
using MediatR;

namespace FairBank.Accounts.Api.Endpoints;

public static class CardEndpoints
{
    public static RouteGroupBuilder MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Cards");

        // POST /api/v1/accounts/{accountId:guid}/cards — issue a new card
        group.MapPost("/accounts/{accountId:guid}/cards", async (Guid accountId, IssueCardCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = accountId });
            return Results.Created($"/api/v1/cards/{result.Id}", result);
        })
        .WithName("IssueCard")
        .Produces<CardResponse>(StatusCodes.Status201Created);

        // GET /api/v1/accounts/{accountId:guid}/cards — list cards for an account
        group.MapGet("/accounts/{accountId:guid}/cards", async (Guid accountId, ISender sender) =>
        {
            var result = await sender.Send(new GetCardsByAccountQuery(accountId));
            return Results.Ok(result);
        })
        .WithName("GetCardsByAccount")
        .Produces<IReadOnlyList<CardResponse>>(StatusCodes.Status200OK);

        // POST /api/v1/cards/{id:guid}/freeze — freeze a card
        group.MapPost("/cards/{id:guid}/freeze", async (Guid id, ISender sender) =>
        {
            await sender.Send(new FreezeCardCommand(id));
            return Results.NoContent();
        })
        .WithName("FreezeCard")
        .Produces(StatusCodes.Status204NoContent);

        // POST /api/v1/cards/{id:guid}/unfreeze — unfreeze a card
        group.MapPost("/cards/{id:guid}/unfreeze", async (Guid id, ISender sender) =>
        {
            await sender.Send(new UnfreezeCardCommand(id));
            return Results.NoContent();
        })
        .WithName("UnfreezeCard")
        .Produces(StatusCodes.Status204NoContent);

        // PUT /api/v1/cards/{id:guid}/limits — set card spending limits
        group.MapPut("/cards/{id:guid}/limits", async (Guid id, SetCardLimitsCommand command, ISender sender) =>
        {
            await sender.Send(command with { CardId = id });
            return Results.NoContent();
        })
        .WithName("SetCardLimits")
        .Produces(StatusCodes.Status204NoContent);

        // PUT /api/v1/cards/{id:guid}/settings — update card settings
        group.MapPut("/cards/{id:guid}/settings", async (Guid id, UpdateCardSettingsCommand command, ISender sender) =>
        {
            await sender.Send(command with { CardId = id });
            return Results.NoContent();
        })
        .WithName("UpdateCardSettings")
        .Produces(StatusCodes.Status204NoContent);

        // DELETE /api/v1/cards/{id:guid} — deactivate a card
        group.MapDelete("/cards/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateCardCommand(id));
            return Results.NoContent();
        })
        .WithName("DeactivateCard")
        .Produces(StatusCodes.Status204NoContent);

        return group;
    }
}
