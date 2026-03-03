using FairBank.Accounts.Application.Commands.ApproveTransaction;
using FairBank.Accounts.Application.Commands.CreateAccount;
using FairBank.Accounts.Application.Commands.DepositMoney;
using FairBank.Accounts.Application.Commands.RejectTransaction;
using FairBank.Accounts.Application.Commands.SetSpendingLimit;
using FairBank.Accounts.Application.Commands.WithdrawMoney;
using FairBank.Accounts.Application.Queries.GetAccountById;
using FairBank.Accounts.Application.Queries.GetAccountByNumber;
using FairBank.Accounts.Application.Queries.GetPendingTransactions;
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

        group.MapGet("/by-number/{accountNumber}", async (string accountNumber, ISender sender) =>
        {
            var result = await sender.Send(new GetAccountByNumberQuery(accountNumber));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetAccountByNumber")
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

        // Spending limits
        group.MapPost("/{id:guid}/limits", async (Guid id, SetSpendingLimitCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("SetSpendingLimit")
        .Produces(StatusCodes.Status200OK);

        // Pending transactions
        group.MapGet("/{id:guid}/pending", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPendingTransactionsQuery(id));
            return Results.Ok(result);
        })
        .WithName("GetPendingTransactions")
        .Produces(StatusCodes.Status200OK);

        // Approve/Reject pending transactions
        var pendingGroup = app.MapGroup("/api/v1/accounts/pending")
            .WithTags("PendingTransactions");

        pendingGroup.MapPost("/{id:guid}/approve", async (Guid id, ApproveTransactionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { TransactionId = id });
            return Results.Ok(result);
        })
        .WithName("ApproveTransaction")
        .Produces(StatusCodes.Status200OK);

        pendingGroup.MapPost("/{id:guid}/reject", async (Guid id, RejectTransactionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { TransactionId = id });
            return Results.Ok(result);
        })
        .WithName("RejectTransaction")
        .Produces(StatusCodes.Status200OK);

        return group;
    }
}
