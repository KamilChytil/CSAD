using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.LoginUser;

public sealed class LoginUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
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
            return null;
        }

        var user = await userRepository.GetByEmailAsync(email, ct);

        if (user is null || !user.IsActive)
            return null;

        // Server-side lockout check (throws so the endpoint can return 429)
        if (user.IsLockedOut)
            throw new UserLockedOutException(user.LockedUntil!.Value);

        // BCrypt password verification
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin();
            await unitOfWork.SaveChangesAsync(ct);

            // After recording, throw lockout if threshold just crossed
            if (user.IsLockedOut)
                throw new UserLockedOutException(user.LockedUntil!.Value);

            return null;
        }

        // Successful login — create new session (invalidates any previous session)
        var sessionId = Guid.NewGuid();
        user.RecordSuccessfulLogin(sessionId);
        await unitOfWork.SaveChangesAsync(ct);

        var token = SessionTokenHelper.Encode(user.Id, sessionId);
        var expiresAt = DateTime.UtcNow.AddHours(8);

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
