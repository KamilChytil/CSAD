using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;
using FairBank.Identity.Application.Ports;
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using FairBank.SharedKernel.Logging;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    IAuditLogger auditLogger,
    ISender sender)
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
            dateOfBirth: DateOnly.TryParse(request.DateOfBirth, out var dob) ? dob : null,
            phoneNumber: phoneNumber,
            address: address);

        user.GenerateEmailVerificationToken();
        await userRepository.AddAsync(user, ct);

        auditLogger.LogSecurityEvent("Register", "Success", user.Id, details: $"Email={user.Email.Value}");

        await sender.Send(new RecordAuditLogCommand(
            Action: "UserRegistered",
            EntityName: "User",
            EntityId: user.Id.ToString(),
            Details: $"{user.Email.Value} ({user.Role})"
        ), ct);

        // Send verification email (fire-and-forget — failures logged but don't block registration)
        if (user.EmailVerificationToken is not null)
        {
            await emailSender.SendEmailVerificationAsync(
                user.Email.Value,
                user.EmailVerificationToken,
                ct);
        }

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
