using FairBank.Payments.Application.Templates.Commands.CreateTemplate;
using FairBank.Payments.Application.Templates.Commands.DeleteTemplate;
using FairBank.Payments.Application.Templates.Queries.GetTemplatesByAccount;
using MediatR;

namespace FairBank.Payments.Api.Endpoints;

public static class PaymentTemplateEndpoints
{
    public static void MapPaymentTemplateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/payment-templates").WithTags("Payment Templates");

        group.MapPost("/", async (CreateTemplateCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/payment-templates/{result.Id}", result);
        })
        .WithName("CreatePaymentTemplate")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapGet("/account/{accountId:guid}", async (Guid accountId, ISender sender) =>
        {
            var result = await sender.Send(new GetTemplatesByAccountQuery(accountId));
            return Results.Ok(result);
        })
        .WithName("GetPaymentTemplatesByAccount")
        .Produces(StatusCodes.Status200OK);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteTemplateCommand(id));
            return Results.NoContent();
        })
        .WithName("DeletePaymentTemplate")
        .Produces(StatusCodes.Status204NoContent);
    }
}
