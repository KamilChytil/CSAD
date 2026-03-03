using FairBank.Payments.Application.Payments.Commands.SendPayment;
using FairBank.Payments.Application.Payments.Queries.GetPaymentsByAccount;
using MediatR;

namespace FairBank.Payments.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/payments").WithTags("Payments");

        group.MapPost("/", async (SendPaymentCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/payments/{result.Id}", result);
        })
        .WithName("SendPayment")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapGet("/account/{accountId:guid}", async (Guid accountId, ISender sender, int? limit) =>
        {
            var result = await sender.Send(new GetPaymentsByAccountQuery(accountId, limit ?? 50));
            return Results.Ok(result);
        })
        .WithName("GetPaymentsByAccount")
        .Produces(StatusCodes.Status200OK);
    }
}
