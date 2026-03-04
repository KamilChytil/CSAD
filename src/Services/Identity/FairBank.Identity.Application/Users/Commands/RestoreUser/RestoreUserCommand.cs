using MediatR;

namespace FairBank.Identity.Application.Users.Commands.RestoreUser;

public sealed record RestoreUserCommand(Guid UserId) : IRequest;
