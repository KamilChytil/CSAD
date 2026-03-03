using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Enums;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    UserRole Role = UserRole.Client) : IRequest<UserResponse>;
