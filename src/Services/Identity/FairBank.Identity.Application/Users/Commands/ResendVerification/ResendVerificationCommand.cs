using MediatR;

namespace FairBank.Identity.Application.Users.Commands.ResendVerification;

public sealed record ResendVerificationCommand(string Email) : IRequest;
