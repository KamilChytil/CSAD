using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterUserCommand, UserResponse>
{
    public async Task<UserResponse> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        var email = Email.Create(request.Email);

        if (await userRepository.ExistsWithEmailAsync(email, ct))
            throw new InvalidOperationException($"User with email '{request.Email}' already exists.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        var user = User.Create(
            request.FirstName,
            request.LastName,
            email,
            passwordHash,
            request.Role);

        await userRepository.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new UserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email.Value,
            user.Role,
            user.IsActive,
            user.CreatedAt);
    }
}
