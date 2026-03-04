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

        var phoneNumber = !string.IsNullOrWhiteSpace(request.Phone)
            ? PhoneNumber.Create(request.Phone)
            : null;

        var address = !string.IsNullOrWhiteSpace(request.Street)
            ? Address.Create(request.Street!, request.City!, request.ZipCode!, request.Country!)
            : null;

        var user = User.Create(
            request.FirstName,
            request.LastName,
            email,
            passwordHash,
            request.Role,
            personalIdNumber: request.PersonalIdNumber,
            dateOfBirth: request.DateOfBirth,
            phoneNumber: phoneNumber,
            address: address);

        await userRepository.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new UserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email.Value,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.PersonalIdNumber,
            user.DateOfBirth,
            user.PhoneNumber?.Value,
            user.Address?.Street,
            user.Address?.City,
            user.Address?.ZipCode,
            user.Address?.Country,
            user.IsEmailVerified,
            user.ParentId);
    }
}
