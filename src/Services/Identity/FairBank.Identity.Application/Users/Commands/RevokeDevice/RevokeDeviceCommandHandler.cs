using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.RevokeDevice;

public sealed class RevokeDeviceCommandHandler(
    IUserDeviceRepository deviceRepo,
    IUserRepository userRepo,
    IUnitOfWork unitOfWork,
    ISender sender) : IRequestHandler<RevokeDeviceCommand>
{
    public async Task Handle(RevokeDeviceCommand request, CancellationToken ct)
    {
        var device = await deviceRepo.GetByIdAsync(request.DeviceId, ct)
            ?? throw new InvalidOperationException("Device not found.");

        if (device.UserId != request.UserId)
            throw new InvalidOperationException("Device does not belong to user.");

        // If device has an active session, invalidate it
        if (device.SessionId.HasValue)
        {
            var user = await userRepo.GetByIdAsync(device.UserId, ct);
            if (user is not null && user.ActiveSessionId == device.SessionId)
            {
                user.InvalidateSession(device.SessionId.Value);
                await userRepo.UpdateAsync(user, ct);
            }
        }

        device.Revoke();
        await deviceRepo.UpdateAsync(device, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await sender.Send(new RecordAuditLogCommand(
            "RevokeDevice",
            request.UserId,
            null, // User email might not be handy
            "UserDevice",
            device.Id.ToString(),
            $"Revoked device {device.DeviceName}"), ct);
    }
}
