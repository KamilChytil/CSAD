using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.EnableTwoFactor;

public sealed class EnableTwoFactorCommandHandler(
    IUserRepository userRepo,
    ITwoFactorAuthRepository tfaRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<EnableTwoFactorCommand, string[]>
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
            backupCodes.Select(c => BCrypt.Net.BCrypt.HashPassword(c, workFactor: 10)).ToArray());

        tfa.Enable(hashedCodes);
        await tfaRepo.UpdateAsync(tfa, ct);

        user.EnableTwoFactor();
        await userRepo.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return backupCodes; // Return plaintext codes once, user must save them
    }
}
