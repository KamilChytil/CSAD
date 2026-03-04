using MediatR;

namespace FairBank.Identity.Application.Users.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest;
