using FairBank.Payments.Application.Commands.ParseQrPayment;
using FairBank.Payments.Application.Payments.Commands.SendPayment;
using FairBank.Payments.Application.Payments.Commands.SetPaymentCategory;
using FairBank.Payments.Application.Payments.Queries.ExportPayments;
using FairBank.Payments.Application.Payments.Queries.GetPaymentsByAccount;
using FairBank.Payments.Application.Payments.Queries.GetPaymentStatistics;
using FairBank.Payments.Application.Payments.Queries.SearchPayments;
using FairBank.Payments.Application.Queries.GenerateQrCode;
using FairBank.Payments.Application.Services;
using MediatR;

namespace FairBank.Payments.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/payments").WithTags("Payments");

        group.MapPost("/", async (SendPaymentCommand command, ISender sender) =>
        {
            try
            {
                var result = await sender.Send(command);
                return Results.Created($"/api/v1/payments/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SendPayment")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .ProducesValidationProblem();

        group.MapGet("/account/{accountId:guid}", async (Guid accountId, ISender sender, int? limit) =>
        {
            var result = await sender.Send(new GetPaymentsByAccountQuery(accountId, limit ?? 50));
            return Results.Ok(result);
        })
        .WithName("GetPaymentsByAccount")
        .Produces(StatusCodes.Status200OK);

        group.MapGet("/qr-code", async (
            string accountNumber,
            decimal? amount,
            string? currency,
            string? message,
            ISender sender) =>
        {
            var result = await sender.Send(new GenerateQrCodeQuery(
                accountNumber, amount, currency ?? "CZK", message));
            return Results.Ok(result);
        })
        .WithName("GenerateQrCode")
        .Produces<QrCodeResult>(StatusCodes.Status200OK);

        group.MapPost("/parse-qr", async (ParseQrPaymentCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result is not null ? Results.Ok(result) : Results.BadRequest("Invalid SPAYD format.");
        })
        .WithName("ParseQrPayment");

        group.MapGet("/account/{accountId:guid}/search", async (
            Guid accountId,
            [AsParameters] SearchPaymentsQuery query,
            ISender sender) =>
        {
            var result = await sender.Send(query with { AccountId = accountId });
            return Results.Ok(result);
        })
        .WithName("SearchPayments");

        group.MapGet("/account/{accountId:guid}/statistics", async (
            Guid accountId,
            string? period,
            DateTime? dateFrom,
            DateTime? dateTo,
            ISender sender) =>
        {
            var result = await sender.Send(new GetPaymentStatisticsQuery(accountId, period ?? "monthly", dateFrom, dateTo));
            return Results.Ok(result);
        })
        .WithName("GetPaymentStatistics");

        group.MapGet("/account/{accountId:guid}/export", async (
            Guid accountId,
            string? format,
            DateTime? dateFrom,
            DateTime? dateTo,
            ISender sender) =>
        {
            var result = await sender.Send(new ExportPaymentsQuery(accountId, format ?? "csv", dateFrom, dateTo));
            return Results.File(result.Data, result.ContentType, result.FileName);
        })
        .WithName("ExportPayments");

        group.MapPut("/{id:guid}/category", async (Guid id, SetPaymentCategoryCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { PaymentId = id });
            return Results.Ok(result);
        })
        .WithName("SetPaymentCategory");
    }
}
