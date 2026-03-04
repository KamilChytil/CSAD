using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ISender sender)
    : IRequestHandler<ChangePasswordCommand, bool>
{
    public async Task<bool> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new InvalidOperationException("Current password is incorrect.");

        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.ChangePassword(request.CurrentPassword, newHash);

        await userRepository.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await sender.Send(new RecordAuditLogCommand(
            "ChangePassword",
            user.Id,
            user.Email.Value,
            "User",
            user.Id.ToString(),
            "Successfully changed password"), ct);

        return true;
    }
}
