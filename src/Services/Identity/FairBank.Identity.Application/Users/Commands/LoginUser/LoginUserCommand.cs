using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.LoginUser;

public sealed record LoginUserCommand(
    string Email,
    string Password) : IRequest<UserResponse?>;
