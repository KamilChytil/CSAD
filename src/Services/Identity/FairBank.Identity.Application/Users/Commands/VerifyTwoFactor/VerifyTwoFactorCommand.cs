using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.VerifyTwoFactor;

public sealed record VerifyTwoFactorCommand(
    Guid UserId,
    string Code) : IRequest<LoginResponse>;
