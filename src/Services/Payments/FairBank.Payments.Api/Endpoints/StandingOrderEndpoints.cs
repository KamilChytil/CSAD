using FairBank.Payments.Application.StandingOrders.Commands.CancelStandingOrder;
using FairBank.Payments.Application.StandingOrders.Commands.CreateStandingOrder;
using FairBank.Payments.Application.StandingOrders.Queries.GetStandingOrdersByAccount;
using MediatR;

namespace FairBank.Payments.Api.Endpoints;

public static class StandingOrderEndpoints
{
    public static void MapStandingOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/standing-orders").WithTags("Standing Orders");

        group.MapPost("/", async (CreateStandingOrderCommand command, ISender sender) =>
        {
            try
            {
                var result = await sender.Send(command);
                return Results.Created($"/api/v1/standing-orders/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateStandingOrder")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .ProducesValidationProblem();

        group.MapGet("/account/{accountId:guid}", async (Guid accountId, ISender sender) =>
        {
            var result = await sender.Send(new GetStandingOrdersByAccountQuery(accountId));
            return Results.Ok(result);
        })
        .WithName("GetStandingOrdersByAccount")
        .Produces(StatusCodes.Status200OK);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new CancelStandingOrderCommand(id));
            return Results.NoContent();
        })
        .WithName("CancelStandingOrder")
        .Produces(StatusCodes.Status204NoContent);
    }
}
