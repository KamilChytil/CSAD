using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.RegisterDevice;

public sealed class RegisterDeviceCommandHandler(
    IUserDeviceRepository deviceRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<RegisterDeviceCommand, DeviceResponse>
{
    public async Task<DeviceResponse> Handle(RegisterDeviceCommand request, CancellationToken ct)
    {
        // Check if device already exists (same browser + OS + device type)
        var existing = await deviceRepo.FindByFingerprintAsync(
            request.UserId, request.Browser ?? "", request.OperatingSystem ?? "",
            request.DeviceType ?? "", ct);

        if (existing is not null)
        {
            existing.UpdateActivity(request.IpAddress, request.SessionId);
            await deviceRepo.UpdateAsync(existing, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return MapToResponse(existing);
        }

        var device = UserDevice.Create(
            request.UserId, request.DeviceName, request.DeviceType,
            request.Browser, request.OperatingSystem, request.IpAddress,
            request.SessionId);

        await deviceRepo.AddAsync(device, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapToResponse(device);
    }

    private static DeviceResponse MapToResponse(UserDevice d) => new(
        d.Id, d.DeviceName, d.DeviceType, d.Browser, d.OperatingSystem,
        d.IpAddress, d.IsTrusted, d.IsCurrentDevice, d.LastActiveAt, d.CreatedAt);
}
