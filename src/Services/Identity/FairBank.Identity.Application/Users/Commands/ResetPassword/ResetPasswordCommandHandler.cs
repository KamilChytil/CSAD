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
        var user = await userRepository.GetByPasswordResetTokenAsync(request.Token, ct);

        if (user is null)
            throw new InvalidOperationException("Invalid reset token.");

        var newHash = FairBank.SharedKernel.Security.PasswordHasher.Hash(request.NewPassword);
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
