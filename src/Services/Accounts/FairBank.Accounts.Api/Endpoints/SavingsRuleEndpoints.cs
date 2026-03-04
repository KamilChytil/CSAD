using FairBank.Accounts.Application.Commands.CreateSavingsRule;
using FairBank.Accounts.Application.Commands.ToggleSavingsRule;
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Queries.GetSavingsRulesByAccount;
using MediatR;

namespace FairBank.Accounts.Api.Endpoints;

public static class SavingsRuleEndpoints
{
    public static RouteGroupBuilder MapSavingsRuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("SavingsRules");

        // POST /api/v1/accounts/{accountId:guid}/savings-rules — create a new savings rule
        group.MapPost("/accounts/{accountId:guid}/savings-rules", async (Guid accountId, CreateSavingsRuleCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = accountId });
            return Results.Created($"/api/v1/savings-rules/{result.Id}", result);
        })
        .WithName("CreateSavingsRule")
        .Produces<SavingsRuleResponse>(StatusCodes.Status201Created);

        // GET /api/v1/accounts/{accountId:guid}/savings-rules — list savings rules for an account
        group.MapGet("/accounts/{accountId:guid}/savings-rules", async (Guid accountId, ISender sender) =>
        {
            var result = await sender.Send(new GetSavingsRulesByAccountQuery(accountId));
            return Results.Ok(result);
        })
        .WithName("GetSavingsRulesByAccount")
        .Produces<IReadOnlyList<SavingsRuleResponse>>(StatusCodes.Status200OK);

        // PUT /api/v1/savings-rules/{id:guid}/toggle — toggle a savings rule on/off
        group.MapPut("/savings-rules/{id:guid}/toggle", async (Guid id, ISender sender) =>
        {
            await sender.Send(new ToggleSavingsRuleCommand(id));
            return Results.NoContent();
        })
        .WithName("ToggleSavingsRule")
        .Produces(StatusCodes.Status204NoContent);

        return group;
    }
}
