using MediatR;

namespace FairBank.Identity.Application.Users.Commands.DeleteUser;

public sealed record DeleteUserCommand(Guid UserId) : IRequest;
