using FairBank.Documents.Application.Commands.GenerateStatement;
using FairBank.Documents.Application.Enums;
using MediatR;

namespace FairBank.Documents.Api.Endpoints;

public static class StatementEndpoints
{
    public static RouteGroupBuilder MapStatementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents")
            .WithTags("Documents");

        // GET /api/v1/documents/statements/{accountId}?from=&to=&format=
        group.MapGet("/statements/{accountId:guid}", async (
            Guid accountId,
            DateTime? from,
            DateTime? to,
            string format,
            ISender sender) =>
        {
            if (!Enum.TryParse<StatementFormat>(format, true, out var fmt))
                return Results.BadRequest("format must be pdf, docx or xlsx");

            var command = new GenerateStatementCommand(accountId, from, to, fmt);
            var response = await sender.Send(command);
            return Results.File(response.Content, response.ContentType, response.FileName);
        })
        .WithName("GenerateStatement")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        return group;
    }
}