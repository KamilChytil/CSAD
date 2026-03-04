using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.DisableTwoFactor;

public sealed class DisableTwoFactorCommandHandler(
    IUserRepository userRepo,
    ITwoFactorAuthRepository tfaRepo,
    IUnitOfWork unitOfWork,
    ISender sender) : IRequestHandler<DisableTwoFactorCommand>
{
    public async Task Handle(DisableTwoFactorCommand request, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var tfa = await tfaRepo.GetByUserIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("2FA is not set up.");

        if (!tfa.IsEnabled)
            throw new InvalidOperationException("2FA is not enabled.");

        // Verify with TOTP code or backup code
        if (!TotpHelper.VerifyCode(tfa.SecretKey, request.Code))
        {
            // Try backup codes
            if (!VerifyBackupCode(tfa, request.Code))
                throw new InvalidOperationException("Invalid code.");
        }

        tfa.Disable();
        await tfaRepo.UpdateAsync(tfa, ct);

        user.DisableTwoFactor();
        await userRepo.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await sender.Send(new RecordAuditLogCommand(
            "DisableTwoFactor",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            "Disabled two-factor authentication"), ct);
    }

    private static bool VerifyBackupCode(TwoFactorAuth tfa, string code)
    {
        if (string.IsNullOrEmpty(tfa.BackupCodes)) return false;

        var hashedCodes = System.Text.Json.JsonSerializer.Deserialize<string[]>(tfa.BackupCodes);
        return hashedCodes?.Any(h => FairBank.SharedKernel.Security.PasswordHasher.Verify(code, h)) == true;
    }
}
