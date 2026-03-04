using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Application.Users.Commands.ActivateUser;
using FairBank.Identity.Application.Users.Commands.ChangeEmail;
using FairBank.Identity.Application.Users.Commands.ChangePassword;
using FairBank.Identity.Application.Users.Commands.DeactivateUser;
using FairBank.Identity.Application.Users.Commands.DeleteUser;
using FairBank.Identity.Application.Users.Commands.UpdateUserRole;
using FairBank.Identity.Application.Users.Commands.CreateChild;
using FairBank.Identity.Application.Users.Commands.ForgotPassword;
using FairBank.Identity.Application.Users.Commands.LoginUser;
using FairBank.Identity.Application.Users.Commands.LogoutUser;
using FairBank.Identity.Application.Users.Commands.RegisterUser;
using FairBank.Identity.Application.Users.Commands.ResendVerification;
using FairBank.Identity.Application.Users.Commands.ResetPassword;
using FairBank.Identity.Application.Users.Commands.SetSecuritySettings;
using FairBank.Identity.Application.Users.Commands.SetupTwoFactor;
using FairBank.Identity.Application.Users.Commands.EnableTwoFactor;
using FairBank.Identity.Application.Users.Commands.DisableTwoFactor;
using FairBank.Identity.Application.Users.Commands.VerifyTwoFactor;
using FairBank.Identity.Application.Users.Commands.VerifyEmail;
using FairBank.Identity.Application.Users.Commands.RegisterDevice;
using FairBank.Identity.Application.Users.Commands.RevokeDevice;
using FairBank.Identity.Application.Users.Commands.TrustDevice;
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Application.Users.Queries.GetAllUsers;
using FairBank.Identity.Application.Users.Queries.GetChildren;
using FairBank.Identity.Application.Users.Queries.GetBankers;
using FairBank.Identity.Application.Users.Queries.GetUserById;
using FairBank.Identity.Application.Users.Queries.GetDevices;
using FairBank.Identity.Application.Users.Queries.GetSecuritySettings;
using FairBank.Identity.Application.Users.Queries.ValidateSession;
using FairBank.Identity.Application.Audit.Queries.GetAuditLogs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using MediatR;
using System.Net.Http.Json;

namespace FairBank.Identity.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("Users");

        group.MapPost("/register", async (RegisterUserCommand command, ISender sender, IHttpClientFactory httpClientFactory) =>
        {
            var result = await sender.Send(command);

            // Auto-provision a checking + savings CZK account for the new user (fire-and-forget)
            try
            {
                var accountsClient = httpClientFactory.CreateClient("accounts-api");
                await accountsClient.PostAsJsonAsync("/api/v1/accounts", new { OwnerId = result.Id, Currency = "CZK", AccountType = 0 }); // Checking
                await accountsClient.PostAsJsonAsync("/api/v1/accounts", new { OwnerId = result.Id, Currency = "CZK", AccountType = 1 }); // Savings
            }
            catch { /* log silently */ }

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

        group.MapPost("/{parentId:guid}/children", async (Guid parentId, CreateChildCommand command, ISender sender, IHttpClientFactory httpClientFactory) =>
        {
            var result = await sender.Send(command with { ParentId = parentId });

            try
            {
                var accountsClient = httpClientFactory.CreateClient("accounts-api");
                await accountsClient.PostAsJsonAsync("/api/v1/accounts", new { OwnerId = result.Id, Currency = "CZK", AccountType = 0 }); // Checking
                await accountsClient.PostAsJsonAsync("/api/v1/accounts", new { OwnerId = result.Id, Currency = "CZK", AccountType = 1 }); // Savings
            }
            catch { /* fire-and-forget */ }

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

        group.MapGet("/bankers", async (ISender sender) =>
        {
            var result = await sender.Send(new GetBankersQuery());
            return Results.Ok(result);
        })
        .WithName("GetBankers")
        .Produces<IEnumerable<UserResponse>>(StatusCodes.Status200OK);

        group.MapPost("/verify-email", async (VerifyEmailCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Ok(new { verified = result });
        })
        .WithName("VerifyEmail")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/resend-verification", async (ResendVerificationCommand command, ISender sender) =>
        {
            await sender.Send(command);
            return Results.Ok(new { sent = true });
        })
        .WithName("ResendVerification")
        .Produces(StatusCodes.Status200OK);

        group.MapPost("/forgot-password", async (ForgotPasswordCommand command, ISender sender) =>
        {
            await sender.Send(command);
            return Results.Ok(new { sent = true });
        })
        .WithName("ForgotPassword")
        .Produces(StatusCodes.Status200OK);

        group.MapPost("/reset-password", async (ResetPasswordCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Ok(new { reset = result });
        })
        .WithName("ResetPassword")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/change-password", async (ChangePasswordCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Ok(new { changed = result });
        })
        .WithName("ChangePassword")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        // ── Security Settings ────────────────────────────────
        group.MapPut("/{id:guid}/security-settings", async (Guid id, SetSecuritySettingsCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { UserId = id });
            return Results.Ok(new { updated = result });
        })
        .WithName("SetSecuritySettings")
        .Produces(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}/security-settings", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSecuritySettingsQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetSecuritySettings")
        .Produces<SecuritySettingsResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // ── Profile Editing ──────────────────────────────────

        group.MapPut("/{id:guid}/email", async (Guid id, ChangeEmailCommand command, ISender sender) =>
        {
            await sender.Send(command with { UserId = id });
            return Results.NoContent();
        })
        .WithName("ChangeEmail")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}/password", async (Guid id, ChangePasswordCommand command, ISender sender) =>
        {
            await sender.Send(command with { UserId = id });
            return Results.NoContent();
        })
        .WithName("ChangePasswordPut")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest);

        // ── Two-Factor Authentication ────────────────────────

        group.MapPost("/2fa/setup", async (Guid userId, ISender sender) =>
        {
            try
            {
                var result = await sender.Send(new SetupTwoFactorCommand(userId));
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SetupTwoFactor")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/2fa/enable", async (EnableTwoFactorCommand command, ISender sender) =>
        {
            try
            {
                var backupCodes = await sender.Send(command);
                return Results.Ok(new { backupCodes });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("EnableTwoFactor")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/2fa/disable", async (DisableTwoFactorCommand command, ISender sender) =>
        {
            try
            {
                await sender.Send(command);
                return Results.Ok(new { disabled = true });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("DisableTwoFactor")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/2fa/verify", async (VerifyTwoFactorCommand command, ISender sender) =>
        {
            try
            {
                var result = await sender.Send(command);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("VerifyTwoFactor")
        .Produces<LoginResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        // ── Admin User Management ────────────────────────────

        group.MapGet("/", async (ISender sender, int page = 1, int pageSize = 20, UserRole? role = null, string? search = null) =>
            Results.Ok(await sender.Send(new GetAllUsersQuery(page, pageSize, role, search))))
        .WithName("GetAllUsers")
        .Produces<PagedUsersResponse>(StatusCodes.Status200OK);

        group.MapPut("/{id:guid}/role", async (Guid id, UpdateUserRoleCommand command, ISender sender) =>
        {
            await sender.Send(command with { UserId = id });
            return Results.NoContent();
        })
        .WithName("UpdateUserRole")
        .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateUserCommand(id));
            return Results.NoContent();
        })
        .WithName("DeactivateUser")
        .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
        {
            await sender.Send(new ActivateUserCommand(id));
            return Results.NoContent();
        })
        .WithName("ActivateUser")
        .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteUserCommand(id));
            return Results.NoContent();
        })
        .WithName("DeleteUser")
        .Produces(StatusCodes.Status204NoContent);

        // ── Device Management ────────────────────────────────

        group.MapGet("/{userId:guid}/devices", async (Guid userId, ISender sender) =>
        {
            var result = await sender.Send(new GetDevicesQuery(userId));
            return Results.Ok(result);
        })
        .WithName("GetDevices")
        .Produces(StatusCodes.Status200OK);

        group.MapPost("/devices", async (RegisterDeviceCommand command, ISender sender) =>
        {
            try
            {
                var result = await sender.Send(command);
                return Results.Created($"/api/v1/users/devices/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RegisterDevice")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapDelete("/devices/{id:guid}", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            var token = ExtractBearerToken(ctx);
            if (token is null || !SessionTokenHelper.TryDecode(token, out var userId, out _))
                return Results.Unauthorized();

            try
            {
                await sender.Send(new RevokeDeviceCommand(id, userId));
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RevokeDevice")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/devices/{id:guid}/trust", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            var token = ExtractBearerToken(ctx);
            if (token is null || !SessionTokenHelper.TryDecode(token, out var userId, out _))
                return Results.Unauthorized();

            try
            {
                await sender.Send(new TrustDeviceCommand(id, userId));
                return Results.Ok(new { trusted = true });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("TrustDevice")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        // ── Admin Audit Logs ─────────────────────────────────

        group.MapGet("/admin/audit-logs", async (ISender sender, int page = 1, int pageSize = 20) =>
            Results.Ok(await sender.Send(new GetAuditLogsQuery(page, pageSize))))
        .WithName("GetAuditLogs")
        .Produces<PagedAuditLogsResponse>(StatusCodes.Status200OK);

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

