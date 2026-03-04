using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;
using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using FairBank.SharedKernel.Logging;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.LoginUser;

public sealed class LoginUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IAuditLogger auditLogger,
    ISender sender)
    : IRequestHandler<LoginUserCommand, LoginResponse?>
{
    public async Task<LoginResponse?> Handle(LoginUserCommand request, CancellationToken ct)
    {
        Email email;
        try
        {
            email = Email.Create(request.Email);
        }
        catch (ArgumentException)
        {
            auditLogger.LogSecurityEvent("Login", "Failed", details: $"InvalidEmail={request.Email}");
            return null;
        }

        var user = await userRepository.GetByEmailAsync(email, ct);

        if (user is null || !user.IsActive)
        {
            auditLogger.LogSecurityEvent("Login", "Failed", details: $"Email={request.Email}");
            return null;
        }

        // Server-side lockout check (throws so the endpoint can return 429)
        if (user.IsLockedOut)
        {
            auditLogger.LogSecurityEvent("Login", "Locked", user.Id, details: $"LockedUntil={user.LockedUntil}");
            throw new UserLockedOutException(user.LockedUntil!.Value);
        }

        // BCrypt password verification
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin();

            await sender.Send(new RecordAuditLogCommand(
                Action: "LoginFailed",
                UserId: user.Id,
                UserEmail: user.Email.Value,
                Details: "Invalid password"
            ), ct);

            await unitOfWork.SaveChangesAsync(ct);

            // After recording, throw lockout if threshold just crossed
            if (user.IsLockedOut)
            {
                auditLogger.LogSecurityEvent("Login", "Locked", user.Id, details: $"LockedUntil={user.LockedUntil}");
                throw new UserLockedOutException(user.LockedUntil!.Value);
            }

            auditLogger.LogSecurityEvent("Login", "Failed", user.Id, details: "InvalidPassword");
            return null;
        }

        // Email verification check — only enforce for users who have a pending verification token
        // (users created before this feature have no token and are allowed in)
        if (!user.IsEmailVerified && user.EmailVerificationToken is not null)
            throw new InvalidOperationException("Email address has not been verified.");

        // Two-factor authentication check — if enabled, return partial response
        // so the client can prompt for the TOTP code
        if (user.IsTwoFactorEnabled)
        {
            return new LoginResponse(
                Token: "",
                UserId: user.Id,
                Email: user.Email.Value,
                FirstName: user.FirstName,
                LastName: user.LastName,
                Role: user.Role.ToString(),
                SessionId: Guid.Empty,
                ExpiresAt: DateTime.MinValue)
            {
                RequiresTwoFactor = true
            };
        }

        // Successful login — create new session (invalidates any previous session)
        var sessionId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddHours(8);
        user.RecordSuccessfulLogin(sessionId, expiresAt);

        await sender.Send(new RecordAuditLogCommand(
            Action: "LoginSuccess",
            UserId: user.Id,
            UserEmail: user.Email.Value
        ), ct);

        await unitOfWork.SaveChangesAsync(ct);

        var token = SessionTokenHelper.Encode(user.Id, sessionId);

        auditLogger.LogSecurityEvent("Login", "Success", user.Id);

        return new LoginResponse(
            Token: token,
            UserId: user.Id,
            Email: user.Email.Value,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Role: user.Role.ToString(),
            SessionId: sessionId,
            ExpiresAt: expiresAt);
    }
}
