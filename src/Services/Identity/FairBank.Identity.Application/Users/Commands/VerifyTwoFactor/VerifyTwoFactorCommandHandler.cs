using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.VerifyTwoFactor;

public sealed class VerifyTwoFactorCommandHandler(
    IUserRepository userRepo,
    ITwoFactorAuthRepository tfaRepo,
    IUnitOfWork unitOfWork,
    ISender sender) : IRequestHandler<VerifyTwoFactorCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(VerifyTwoFactorCommand request, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var tfa = await tfaRepo.GetByUserIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("2FA not configured.");

        var isValid = TotpHelper.VerifyCode(tfa.SecretKey, request.Code);

        // Try backup code if TOTP fails
        if (!isValid && !string.IsNullOrEmpty(tfa.BackupCodes))
        {
            var hashedCodes = System.Text.Json.JsonSerializer.Deserialize<string[]>(tfa.BackupCodes);
            if (hashedCodes is not null)
            {
                for (var i = 0; i < hashedCodes.Length; i++)
                {
                    if (!string.IsNullOrEmpty(hashedCodes[i]) &&
                        BCrypt.Net.BCrypt.Verify(request.Code, hashedCodes[i]))
                    {
                        isValid = true;
                        // Invalidate used backup code
                        hashedCodes[i] = "";
                        tfa.RegenerateBackupCodes(
                            System.Text.Json.JsonSerializer.Serialize(hashedCodes));
                        await tfaRepo.UpdateAsync(tfa, ct);
                        break;
                    }
                }
            }
        }

        if (!isValid)
            throw new InvalidOperationException("Invalid 2FA code.");

        // Complete login - create session
        var sessionId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddHours(8);
        user.RecordSuccessfulLogin(sessionId, expiresAt);
        await userRepo.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var token = SessionTokenHelper.Encode(user.Id, sessionId);

        await sender.Send(new RecordAuditLogCommand(
            "VerifyTwoFactor",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            "Successfully verified 2FA and logged in"), ct);

        return new LoginResponse(
            Token: token,
            UserId: user.Id,
            Email: user.Email.Value,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Role: user.Role.ToString(),
            SessionId: sessionId,
            ExpiresAt: expiresAt);
    }
}
