using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.EnableTwoFactor;

public sealed class EnableTwoFactorCommandHandler(
    IUserRepository userRepo,
    ITwoFactorAuthRepository tfaRepo,
    IUnitOfWork unitOfWork,
    ISender sender) : IRequestHandler<EnableTwoFactorCommand, string[]>
{
    public async Task<string[]> Handle(EnableTwoFactorCommand request, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var tfa = await tfaRepo.GetByUserIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("2FA setup not found. Call setup first.");

        if (tfa.IsEnabled)
            throw new InvalidOperationException("2FA is already enabled.");

        if (!TotpHelper.VerifyCode(tfa.SecretKey, request.Code))
            throw new InvalidOperationException("Invalid TOTP code.");

        // Generate backup codes
        var backupCodes = TotpHelper.GenerateBackupCodes();
        var hashedCodes = System.Text.Json.JsonSerializer.Serialize(
            backupCodes.Select(c => FairBank.SharedKernel.Security.PasswordHasher.Hash(c)).ToArray());

        tfa.Enable(hashedCodes);
        await tfaRepo.UpdateAsync(tfa, ct);

        user.EnableTwoFactor();
        await userRepo.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await sender.Send(new RecordAuditLogCommand(
            "EnableTwoFactor",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            "Enabled two-factor authentication"), ct);

        return backupCodes; // Return plaintext codes once, user must save them
    }
}
