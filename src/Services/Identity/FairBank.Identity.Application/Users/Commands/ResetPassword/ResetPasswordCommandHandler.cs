using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ISender sender)
    : IRequestHandler<ResetPasswordCommand, bool>
{
    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByPasswordResetTokenAsync(request.Token, ct)
            ?? throw new InvalidOperationException("Invalid reset token.");

        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.ResetPassword(request.Token, newHash);

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await sender.Send(new RecordAuditLogCommand(
            "ResetPassword",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            "Successfully reset password"), ct);

        return true;
    }
}
