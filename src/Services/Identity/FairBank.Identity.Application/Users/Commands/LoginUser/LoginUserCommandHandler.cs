using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.LoginUser;

public sealed class LoginUserCommandHandler(IUserRepository userRepository)
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

        var passwordHash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(request.Password)));

        if (user.PasswordHash != passwordHash)
            return null;

        // Dummy token and session for now to satisfy the frontend
        return new LoginResponse(
            Token: "dummy-jwt-token-" + Guid.NewGuid().ToString("N"),
            UserId: user.Id,
            Email: user.Email.Value,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Role: user.Role.ToString(),
            SessionId: Guid.NewGuid(),
            ExpiresAt: DateTime.UtcNow.AddHours(1));
    }
}
