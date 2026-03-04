using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

using FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

namespace FairBank.Identity.Application.Users.Commands.TrustDevice;

public sealed class TrustDeviceCommandHandler(
    IUserDeviceRepository deviceRepo,
    IUnitOfWork unitOfWork,
    ISender sender) : IRequestHandler<TrustDeviceCommand>
{
    public async Task Handle(TrustDeviceCommand request, CancellationToken ct)
    {
        var device = await deviceRepo.GetByIdAsync(request.DeviceId, ct)
            ?? throw new InvalidOperationException("Device not found.");

        if (device.UserId != request.UserId)
            throw new InvalidOperationException("Device does not belong to user.");

        device.MarkTrusted();
        await deviceRepo.UpdateAsync(device, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await sender.Send(new RecordAuditLogCommand(
            "TrustDevice",
            request.UserId,
            null,
            "UserDevice",
            device.Id.ToString(),
            $"Marked device {device.DeviceName} as trusted"), ct);
    }
}
