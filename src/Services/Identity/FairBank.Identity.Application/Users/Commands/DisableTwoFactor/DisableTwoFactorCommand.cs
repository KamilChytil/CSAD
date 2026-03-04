using MediatR;

namespace FairBank.Identity.Application.Users.Commands.DisableTwoFactor;

public sealed record DisableTwoFactorCommand(Guid UserId, string Code) : IRequest;
