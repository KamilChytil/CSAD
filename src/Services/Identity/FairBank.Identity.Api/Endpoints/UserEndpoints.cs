using FairBank.Identity.Application.Users.Commands.CreateChild;
using FairBank.Identity.Application.Users.Commands.RegisterUser;
using FairBank.Identity.Application.Users.Queries.GetChildren;
using FairBank.Identity.Application.Users.Queries.GetUserById;
using MediatR;

namespace FairBank.Identity.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("Users");

        group.MapPost("/register", async (RegisterUserCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/users/{result.Id}", result);
        })
        .WithName("RegisterUser")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetUserByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetUserById")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{parentId:guid}/children", async (Guid parentId, CreateChildCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { ParentId = parentId });
            return Results.Created($"/api/v1/users/{result.Id}", result);
        })
        .WithName("CreateChild")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{parentId:guid}/children", async (Guid parentId, ISender sender) =>
        {
            var result = await sender.Send(new GetChildrenQuery(parentId));
            return Results.Ok(result);
        })
        .WithName("GetChildren")
        .Produces(StatusCodes.Status200OK);

        return group;
    }
}
