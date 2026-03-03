using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Application.Users.Commands.CreateChild;
using FairBank.Identity.Application.Users.Commands.LoginUser;
using FairBank.Identity.Application.Users.Commands.LogoutUser;
using FairBank.Identity.Application.Users.Commands.RegisterUser;
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Application.Users.Queries.GetChildren;
using FairBank.Identity.Application.Users.Queries.GetUserById;
using FairBank.Identity.Application.Users.Queries.ValidateSession;
using FairBank.Identity.Domain.Entities;
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

        // ── Login — returns 401 (wrong creds), 429 (locked), 200 (success) ──
        group.MapPost("/login", async (LoginUserCommand command, ISender sender) =>
        {
            try
            {
                var result = await sender.Send(command);
                return result is not null ? Results.Ok(result) : Results.Unauthorized();
            }
            catch (UserLockedOutException ex)
            {
                var remaining = (int)Math.Ceiling((ex.LockedUntil - DateTime.UtcNow).TotalSeconds);
                return Results.Json(
                    new LoginLockoutResponse(true, ex.LockedUntil, remaining),
                    statusCode: StatusCodes.Status429TooManyRequests);
            }
        })
        .WithName("LoginUser")
        .Produces<LoginResponse>(StatusCodes.Status200OK)
        .Produces<LoginLockoutResponse>(StatusCodes.Status429TooManyRequests)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status400BadRequest);

        // ── Logout — invalidates the active session server-side ──
        group.MapPost("/logout", async (HttpContext ctx, ISender sender) =>
        {
            var token = ExtractBearerToken(ctx);
            if (token is null || !SessionTokenHelper.TryDecode(token, out var userId, out var sessionId))
                return Results.Unauthorized();

            await sender.Send(new LogoutUserCommand(userId, sessionId));
            return Results.NoContent();
        })
        .WithName("LogoutUser")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);

        // ── Session validate — used by frontend AuthGuard ──
        group.MapGet("/session/validate", async (HttpContext ctx, ISender sender) =>
        {
            var token = ExtractBearerToken(ctx);
            if (token is null || !SessionTokenHelper.TryDecode(token, out var userId, out var sessionId))
                return Results.Unauthorized();

            var valid = await sender.Send(new ValidateSessionQuery(userId, sessionId));
            return valid ? Results.Ok(new { valid = true }) : Results.Unauthorized();
        })
        .WithName("ValidateSession")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

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

    private static string? ExtractBearerToken(HttpContext ctx)
    {
        var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (auth is null || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;
        return auth["Bearer ".Length..].Trim();
    }
}

