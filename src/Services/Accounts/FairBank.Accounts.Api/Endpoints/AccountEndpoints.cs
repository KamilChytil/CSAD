using FairBank.Accounts.Application.Commands.CreateAccount;
using FairBank.Accounts.Application.Commands.DepositMoney;
using FairBank.Accounts.Application.Commands.WithdrawMoney;
using FairBank.Accounts.Application.Queries.GetAccountById;
using MediatR;

namespace FairBank.Accounts.Api.Endpoints;

public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounts")
            .WithTags("Accounts");

        group.MapPost("/", async (CreateAccountCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/accounts/{result.Id}", result);
        })
        .WithName("CreateAccount")
        .Produces(StatusCodes.Status201Created);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAccountByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetAccountById")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/deposit", async (Guid id, DepositMoneyCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("DepositMoney")
        .Produces(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/withdraw", async (Guid id, WithdrawMoneyCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("WithdrawMoney")
        .Produces(StatusCodes.Status200OK);

        return group;
    }
}
