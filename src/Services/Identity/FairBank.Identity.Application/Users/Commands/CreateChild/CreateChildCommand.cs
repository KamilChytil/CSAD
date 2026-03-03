using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.CreateChild;

public sealed record CreateChildCommand(
    Guid ParentId,
    string FirstName,
    string LastName,
    string Email,
    string Password) : IRequest<UserResponse>;
