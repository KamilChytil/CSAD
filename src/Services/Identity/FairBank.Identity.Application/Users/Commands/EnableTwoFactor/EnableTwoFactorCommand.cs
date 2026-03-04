using MediatR;

namespace FairBank.Identity.Application.Users.Commands.EnableTwoFactor;

public sealed record EnableTwoFactorCommand(Guid UserId, string Code) : IRequest<string[]>;
