using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetDevices;

public sealed class GetDevicesQueryHandler(
    IUserDeviceRepository deviceRepo) : IRequestHandler<GetDevicesQuery, IReadOnlyList<DeviceResponse>>
{
    public async Task<IReadOnlyList<DeviceResponse>> Handle(GetDevicesQuery request, CancellationToken ct)
    {
        var devices = await deviceRepo.GetByUserIdAsync(request.UserId, ct);
        return devices.Select(d => new DeviceResponse(
            d.Id, d.DeviceName, d.DeviceType, d.Browser, d.OperatingSystem,
            d.IpAddress, d.IsTrusted, d.IsCurrentDevice, d.LastActiveAt, d.CreatedAt))
            .ToList();
    }
}
