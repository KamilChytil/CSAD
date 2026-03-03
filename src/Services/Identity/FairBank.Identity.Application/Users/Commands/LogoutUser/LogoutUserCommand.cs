using MediatR;

namespace FairBank.Identity.Application.Users.Commands.LogoutUser;

public sealed record LogoutUserCommand(Guid UserId, Guid SessionId) : IRequest;
