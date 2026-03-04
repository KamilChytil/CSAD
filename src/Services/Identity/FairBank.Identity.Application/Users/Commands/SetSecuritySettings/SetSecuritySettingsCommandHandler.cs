using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.SetSecuritySettings;

public sealed class SetSecuritySettingsCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ISender sender)
    : IRequestHandler<SetSecuritySettingsCommand, bool>
{
    public async Task<bool> Handle(SetSecuritySettingsCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        user.UpdateSecuritySettings(
            request.AllowInternationalPayments,
            request.NightTransactionsEnabled,
            request.RequireApprovalAbove);

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await sender.Send(new RecordAuditLogCommand(
            "SetSecuritySettings",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            "Updated security settings"), ct);

        return true;
    }
}
