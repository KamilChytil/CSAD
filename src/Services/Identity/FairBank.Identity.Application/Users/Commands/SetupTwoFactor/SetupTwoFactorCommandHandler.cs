using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.SetupTwoFactor;

public sealed class SetupTwoFactorCommandHandler(
    IUserRepository userRepo,
    ITwoFactorAuthRepository tfaRepo,
    IUnitOfWork unitOfWork,
    ISender sender) : IRequestHandler<SetupTwoFactorCommand, TwoFactorSetupResponse>
{
    public async Task<TwoFactorSetupResponse> Handle(SetupTwoFactorCommand request, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var existing = await tfaRepo.GetByUserIdAsync(request.UserId, ct);
        if (existing is not null && existing.IsEnabled)
        {
            return new TwoFactorSetupResponse(
                Secret: "",
                OtpAuthUri: "",
                IsAlreadyEnabled: true);
        }

        // Delete any existing non-enabled setup
        if (existing is not null)
            await tfaRepo.DeleteByUserIdAsync(request.UserId, ct);

        var secret = TotpHelper.GenerateSecret();
        var otpAuthUri = TotpHelper.GetOtpAuthUri(secret, user.Email.Value);

        var tfa = TwoFactorAuth.Create(request.UserId, secret);
        await tfaRepo.AddAsync(tfa, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await sender.Send(new RecordAuditLogCommand(
            "SetupTwoFactor",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            "Initiated 2FA setup"), ct);

        return new TwoFactorSetupResponse(
            Secret: secret,
            OtpAuthUri: otpAuthUri,
            IsAlreadyEnabled: false);
    }
}
