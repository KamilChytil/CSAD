using FairBank.Payments.Application.Exchange.Commands.AddFavorite;
using FairBank.Payments.Application.Exchange.Commands.ExecuteExchange;
using FairBank.Payments.Application.Exchange.Commands.RemoveFavorite;
using FairBank.Payments.Application.Exchange.Queries.GetExchangeHistory;
using FairBank.Payments.Application.Exchange.Queries.GetExchangeRate;
using FairBank.Payments.Application.Exchange.Queries.GetFavorites;
using MediatR;

namespace FairBank.Payments.Api.Endpoints;

public static class ExchangeEndpoints
{
    public static void MapExchangeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/exchange").WithTags("Exchange");

        group.MapGet("/rate", async (string from, string to, ISender sender) =>
        {
            var result = await sender.Send(new GetExchangeRateQuery(from, to));
            return result is null ? Results.NotFound("Rate not available") : Results.Ok(result);
        }).WithName("GetExchangeRate");

        group.MapPost("/convert", async (ExecuteExchangeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/exchange/history/{result.Id}", result);
        }).WithName("ExecuteExchange")
          .Produces(StatusCodes.Status201Created)
          .ProducesValidationProblem();

        group.MapGet("/history", async (Guid userId, int? limit, ISender sender) =>
        {
            var result = await sender.Send(new GetExchangeHistoryQuery(userId, limit ?? 20));
            return Results.Ok(result);
        }).WithName("GetExchangeHistory");

        group.MapGet("/favorites", async (Guid userId, ISender sender) =>
        {
            var result = await sender.Send(new GetFavoritesQuery(userId));
            return Results.Ok(result);
        }).WithName("GetExchangeFavorites");

        group.MapPost("/favorites", async (AddFavoriteCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/exchange/favorites/{result.Id}", result);
        }).WithName("AddExchangeFavorite")
          .Produces(StatusCodes.Status201Created);

        group.MapDelete("/favorites/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new RemoveFavoriteCommand(id));
            return result ? Results.NoContent() : Results.NotFound();
        }).WithName("RemoveExchangeFavorite");
    }
}
