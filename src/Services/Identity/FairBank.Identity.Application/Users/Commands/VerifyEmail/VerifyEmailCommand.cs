using MediatR;

namespace FairBank.Identity.Application.Users.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(string Token) : IRequest<bool>;
