using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.SetupTwoFactor;

public sealed record SetupTwoFactorCommand(Guid UserId) : IRequest<TwoFactorSetupResponse>;
