using FairBank.Products.Application.Commands.ApproveApplication;
using FairBank.Products.Application.Commands.CancelApplication;
using FairBank.Products.Application.Commands.RejectApplication;
using FairBank.Products.Application.Commands.SubmitApplication;
using FairBank.Products.Application.Queries.GetApplicationById;
using FairBank.Products.Application.Queries.GetPendingApplications;
using FairBank.Products.Application.Queries.GetUserApplications;
using FairBank.SharedKernel.Security;
using MediatR;

namespace FairBank.Products.Api.Endpoints;

public static class ProductApplicationEndpoints
{
    public static void MapProductApplicationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/products/applications").WithTags("Product Applications");

        group.MapPost("/", async (SubmitApplicationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/products/applications/{result.Id}", result);
        })
        .WithName("SubmitApplication")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .RequireAuth();

        group.MapGet("/user/{userId:guid}", async (Guid userId, HttpContext httpContext, ISender sender) =>
        {
            var authUserId = httpContext.GetUserId();
            var role = httpContext.GetUserRole();
            if (role != "Admin" && role != "Banker" && authUserId != userId)
                return Results.Json(new { error = "Forbidden" }, statusCode: 403);

            var result = await sender.Send(new GetUserApplicationsQuery(userId));
            return Results.Ok(result);
        })
        .WithName("GetUserApplications")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();

        group.MapGet("/pending", async (ISender sender) =>
        {
            var result = await sender.Send(new GetPendingApplicationsQuery());
            return Results.Ok(result);
        })
        .WithName("GetPendingApplications")
        .Produces(StatusCodes.Status200OK)
        .RequireRole("Admin", "Banker");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetApplicationByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetApplicationById")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuth();

        group.MapPut("/{id:guid}/approve", async (Guid id, ApproveApplicationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { ApplicationId = id });
            return Results.Ok(result);
        })
        .WithName("ApproveApplication")
        .Produces(StatusCodes.Status200OK)
        .RequireRole("Admin", "Banker");

        group.MapPut("/{id:guid}/reject", async (Guid id, RejectApplicationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { ApplicationId = id });
            return Results.Ok(result);
        })
        .WithName("RejectApplication")
        .Produces(StatusCodes.Status200OK)
        .RequireRole("Admin", "Banker");

        group.MapPut("/{id:guid}/cancel", async (Guid id, CancelApplicationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { ApplicationId = id });
            return Results.Ok(result);
        })
        .WithName("CancelApplication")
        .Produces(StatusCodes.Status200OK)
        .RequireAuth();
    }
}
