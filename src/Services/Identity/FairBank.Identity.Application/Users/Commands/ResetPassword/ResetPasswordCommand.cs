using MediatR;

namespace FairBank.Identity.Application.Users.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<bool>;
