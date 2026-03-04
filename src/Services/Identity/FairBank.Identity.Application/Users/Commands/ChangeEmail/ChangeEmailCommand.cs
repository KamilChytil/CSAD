using MediatR;

namespace FairBank.Identity.Application.Users.Commands.ChangeEmail;

public sealed record ChangeEmailCommand(Guid UserId, string NewEmail) : IRequest;
