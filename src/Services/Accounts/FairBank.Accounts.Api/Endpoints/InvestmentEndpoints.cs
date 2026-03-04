using FairBank.Accounts.Application.Commands.CreateInvestment;
using FairBank.Accounts.Application.Commands.SellInvestment;
using FairBank.Accounts.Application.Commands.UpdateInvestmentValue;
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Queries.GetInvestmentById;
using FairBank.Accounts.Application.Queries.GetInvestmentsByAccount;
using MediatR;

namespace FairBank.Accounts.Api.Endpoints;

public static class InvestmentEndpoints
{
    public static RouteGroupBuilder MapInvestmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Investments");

        // POST /api/v1/accounts/{accountId:guid}/investments — create a new investment
        group.MapPost("/accounts/{accountId:guid}/investments", async (Guid accountId, CreateInvestmentCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = accountId });
            return Results.Created($"/api/v1/investments/{result.Id}", result);
        })
        .WithName("CreateInvestment")
        .Produces<InvestmentResponse>(StatusCodes.Status201Created);

        // GET /api/v1/accounts/{accountId:guid}/investments — list investments for an account
        group.MapGet("/accounts/{accountId:guid}/investments", async (Guid accountId, ISender sender) =>
        {
            var result = await sender.Send(new GetInvestmentsByAccountQuery(accountId));
            return Results.Ok(result);
        })
        .WithName("GetInvestmentsByAccount")
        .Produces<IReadOnlyList<InvestmentResponse>>(StatusCodes.Status200OK);

        // GET /api/v1/investments/{id:guid} — get investment by id
        group.MapGet("/investments/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetInvestmentByIdQuery(id));
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetInvestmentById")
        .Produces<InvestmentResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // PUT /api/v1/investments/{id:guid}/value — update investment value
        group.MapPut("/investments/{id:guid}/value", async (Guid id, UpdateInvestmentValueCommand command, ISender sender) =>
        {
            await sender.Send(command with { InvestmentId = id });
            return Results.NoContent();
        })
        .WithName("UpdateInvestmentValue")
        .Produces(StatusCodes.Status204NoContent);

        // POST /api/v1/investments/{id:guid}/sell — sell an investment
        group.MapPost("/investments/{id:guid}/sell", async (Guid id, ISender sender) =>
        {
            await sender.Send(new SellInvestmentCommand(id));
            return Results.NoContent();
        })
        .WithName("SellInvestment")
        .Produces(StatusCodes.Status204NoContent);

        return group;
    }
}
