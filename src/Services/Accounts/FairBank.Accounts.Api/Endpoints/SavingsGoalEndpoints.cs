using FairBank.Accounts.Application.Commands.CreateSavingsGoal;
using FairBank.Accounts.Application.Commands.DeleteSavingsGoal;
using FairBank.Accounts.Application.Commands.DepositToSavingsGoal;
using FairBank.Accounts.Application.Commands.WithdrawFromSavingsGoal;
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Queries.GetSavingsGoalsByAccount;
using FairBank.SharedKernel.Security;
using MediatR;

namespace FairBank.Accounts.Api.Endpoints;

public static class SavingsGoalEndpoints
{
    public static RouteGroupBuilder MapSavingsGoalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("SavingsGoals");

        // POST /api/v1/accounts/{accountId:guid}/savings-goals — create a new savings goal
        group.MapPost("/accounts/{accountId:guid}/savings-goals", async (Guid accountId, CreateSavingsGoalCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = accountId });
            return Results.Created($"/api/v1/savings-goals/{result.Id}", result);
        })
        .WithName("CreateSavingsGoal")
        .Produces<SavingsGoalResponse>(StatusCodes.Status201Created)
        .RequireAuth();

        // GET /api/v1/accounts/{accountId:guid}/savings-goals — list savings goals for an account
        group.MapGet("/accounts/{accountId:guid}/savings-goals", async (Guid accountId, ISender sender) =>
        {
            var result = await sender.Send(new GetSavingsGoalsByAccountQuery(accountId));
            return Results.Ok(result);
        })
        .WithName("GetSavingsGoalsByAccount")
        .Produces<IReadOnlyList<SavingsGoalResponse>>(StatusCodes.Status200OK)
        .RequireAuth();

        // POST /api/v1/savings-goals/{id:guid}/deposit — deposit to a savings goal
        group.MapPost("/savings-goals/{id:guid}/deposit", async (Guid id, DepositToSavingsGoalCommand command, ISender sender) =>
        {
            await sender.Send(command with { GoalId = id });
            return Results.NoContent();
        })
        .WithName("DepositToSavingsGoal")
        .Produces(StatusCodes.Status204NoContent)
        .RequireAuth();

        // POST /api/v1/savings-goals/{id:guid}/withdraw — withdraw from a savings goal
        group.MapPost("/savings-goals/{id:guid}/withdraw", async (Guid id, WithdrawFromSavingsGoalCommand command, ISender sender) =>
        {
            await sender.Send(command with { GoalId = id });
            return Results.NoContent();
        })
        .WithName("WithdrawFromSavingsGoal")
        .Produces(StatusCodes.Status204NoContent)
        .RequireAuth();

        // DELETE /api/v1/savings-goals/{id:guid} — delete a savings goal
        group.MapDelete("/savings-goals/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteSavingsGoalCommand(id));
            return Results.NoContent();
        })
        .WithName("DeleteSavingsGoal")
        .Produces(StatusCodes.Status204NoContent)
        .RequireAuth();

        return group;
    }
}
