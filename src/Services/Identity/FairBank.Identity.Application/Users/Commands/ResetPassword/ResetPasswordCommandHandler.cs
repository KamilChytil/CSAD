using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using FairBank.SharedKernel.Logging;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IAuditLogger auditLogger)
    : IRequestHandler<ResetPasswordCommand, bool>
{
    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByPasswordResetTokenAsync(request.Token, ct);

        if (user is null)
        {
            auditLogger.LogSecurityEvent("PasswordReset", "Failed", details: "InvalidResetToken");
            throw new InvalidOperationException("Invalid reset token.");
        }

        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.ResetPassword(request.Token, newHash);

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        auditLogger.LogSecurityEvent("PasswordReset", "Success", user.Id);

        return true;
    }
}
