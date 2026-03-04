using FairBank.Identity.Domain.Enums;
using MediatR;
using FairBank.Identity.Application.Users.DTOs;

namespace FairBank.Identity.Application.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    UserRole Role = UserRole.Client,
    string? PersonalIdNumber = null,
    DateOnly? DateOfBirth = null,
    string? Phone = null,
    string? Street = null,
    string? City = null,
    string? ZipCode = null,
    string? Country = null) : IRequest<UserResponse>;
