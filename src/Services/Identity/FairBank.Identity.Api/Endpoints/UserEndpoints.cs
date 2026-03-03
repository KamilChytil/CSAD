using FairBank.Identity.Application.Users.Commands.LoginUser;
using FairBank.Identity.Application.Users.Commands.RegisterUser;
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

        group.MapPost("/login", async (LoginUserCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result is not null ? Results.Ok(result) : Results.Unauthorized();
        })
        .WithName("LoginUser")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetUserByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetUserById")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}
