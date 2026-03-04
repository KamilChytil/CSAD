using FairBank.Accounts.Application.Commands.ApproveTransaction;
using FairBank.Accounts.Application.Commands.CloseAccount;
using FairBank.Accounts.Application.Commands.CreateAccount;
using FairBank.Accounts.Application.Commands.CreatePendingTransaction;
using FairBank.Accounts.Application.Commands.DepositMoney;
using FairBank.Accounts.Application.Commands.RejectTransaction;
using FairBank.Accounts.Application.Commands.RenameAccount;
using FairBank.Accounts.Application.Commands.SetAccountLimits;
using FairBank.Accounts.Application.Commands.SetSpendingLimit;
using FairBank.Accounts.Application.Commands.WithdrawMoney;
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Queries.GetAccountById;
using FairBank.Accounts.Application.Queries.GetAccountByNumber;
using FairBank.Accounts.Application.Queries.GetAccountLimits;
using FairBank.Accounts.Application.Queries.GetAccountsByOwner;
using FairBank.Accounts.Application.Queries.GetPendingTransactions;
using FairBank.Accounts.Application.Queries.GetAccountTransactions;
using FairBank.Accounts.Domain.Enums;
using FairBank.SharedKernel.Security;
using MediatR;

namespace FairBank.Accounts.Api.Endpoints;

public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounts")
            .WithTags("Accounts");

        // GET /api/v1/accounts?ownerId={guid}  — all accounts for an owner
        group.MapGet("/", async (Guid? ownerId, HttpContext httpContext, ISender sender) =>
        {
            if (ownerId is null) return Results.BadRequest("ownerId is required.");

            var authUserId = httpContext.GetUserId();
            var role = httpContext.GetUserRole();
            // Child role: can only query their own accounts.
            // Client (parent), Admin, Banker: can query any account by ownerId.
            // Parents need to view children's accounts for family management.
            if (role == "Child" && ownerId != authUserId)
                return Results.Json(new { error = "Forbidden" }, statusCode: 403);

            var result = await sender.Send(new GetAccountsByOwnerQuery(ownerId.Value));
            return Results.Ok(result);
        })
        .WithName("GetAccountsByOwner")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .RequireAuth();

        group.MapPost("/", async (CreateAccountCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/accounts/{result.Id}", result);
        })
        .WithName("CreateAccount")
        .Produces(StatusCodes.Status201Created)
        .RequireAuth();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAccountByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetAccountById")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuth();

        group.MapGet("/by-number", async (string accountNumber, ISender sender) =>
        {
            var result = await sender.Send(new GetAccountByNumberQuery(accountNumber));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetAccountByNumber")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuth();

        group.MapPost("/{id:guid}/deposit", async (Guid id, DepositMoneyCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("DepositMoney")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        group.MapPost("/{id:guid}/withdraw", async (Guid id, WithdrawMoneyCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("WithdrawMoney")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        // Spending limits
        group.MapPost("/{id:guid}/spending-limit", async (Guid id, SetSpendingLimitCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("SetSpendingLimit")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        // Granular account limits
        group.MapPut("/{id:guid}/limits", async (Guid id, SetAccountLimitsCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("SetAccountLimits")
        .Produces<AccountResponse>(StatusCodes.Status200OK)
        .RequireAuth();

        group.MapGet("/{id:guid}/limits", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAccountLimitsQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetAccountLimits")
        .Produces<AccountLimitsResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuth();

        group.MapPost("/{id:guid}/close", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new CloseAccountCommand(id));
            return Results.Ok(result);
        })
        .WithName("CloseAccount")
        .Produces<AccountResponse>(StatusCodes.Status200OK)
        .RequireAuth();

        group.MapPut("/{id:guid}/alias", async (Guid id, RenameAccountCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("RenameAccount")
        .Produces<AccountResponse>(StatusCodes.Status200OK)
        .RequireAuth();

        // Pending transactions
        group.MapGet("/{id:guid}/pending", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPendingTransactionsQuery(id));
            return Results.Ok(result);
        })
        .WithName("GetPendingTransactions")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        // GET /api/v1/accounts/{id}/transactions?from={}&to={}  — historical ledger
        group.MapGet("/{id:guid}/transactions", async (Guid id, DateTime? from, DateTime? to, ISender sender) =>
        {
            var result = await sender.Send(new GetAccountTransactionsQuery(id, from, to));
            return Results.Ok(result);
        })
        .WithName("GetAccountTransactions")
        .Produces<IReadOnlyList<FairBank.Accounts.Application.DTOs.TransactionDto>>(StatusCodes.Status200OK);

        // Approve/Reject pending transactions
        var pendingGroup = app.MapGroup("/api/v1/accounts/pending")
            .WithTags("PendingTransactions");

        pendingGroup.MapPost("/", async (CreatePendingTransactionRequest req, ISender sender) =>
        {
            if (!Enum.TryParse<Currency>(req.Currency, true, out var currency))
                return Results.BadRequest("Invalid currency.");
            var result = await sender.Send(new CreatePendingTransactionCommand(
                req.AccountId, req.Amount, currency, req.Description, req.RequestedBy));
            return Results.Created($"/api/v1/accounts/pending/{result.Id}", result);
        })
        .WithName("CreatePendingTransaction")
        .RequireAuth();

        pendingGroup.MapPost("/{id:guid}/approve", async (Guid id, ApproveTransactionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { TransactionId = id });
            return Results.Ok(result);
        })
        .WithName("ApproveTransaction")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        pendingGroup.MapPost("/{id:guid}/reject", async (Guid id, RejectTransactionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { TransactionId = id });
            return Results.Ok(result);
        })
        .WithName("RejectTransaction")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        return group;
    }
}

public sealed record CreatePendingTransactionRequest(
    Guid AccountId, decimal Amount, string Currency, string Description, Guid RequestedBy);
