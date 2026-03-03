using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.CreateChild;

public sealed class CreateChildCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateChildCommand, UserResponse>
{
    public async Task<UserResponse> Handle(CreateChildCommand request, CancellationToken ct)
    {
        var parent = await userRepository.GetByIdAsync(request.ParentId, ct)
            ?? throw new InvalidOperationException("Parent user not found.");

        if (parent.Role != UserRole.Client)
            throw new InvalidOperationException("Only clients can create child accounts.");

        var email = Email.Create(request.Email);

        if (await userRepository.ExistsWithEmailAsync(email, ct))
            throw new InvalidOperationException($"User with email '{request.Email}' already exists.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        var child = User.CreateChild(
            request.FirstName,
            request.LastName,
            email,
            passwordHash,
            request.ParentId);

        await userRepository.AddAsync(child, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new UserResponse(
            child.Id, child.FirstName, child.LastName,
            child.Email.Value, child.Role, child.IsActive, child.CreatedAt);
    }
}
