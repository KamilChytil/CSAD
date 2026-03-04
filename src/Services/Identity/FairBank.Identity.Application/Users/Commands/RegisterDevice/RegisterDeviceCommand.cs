using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.RegisterDevice;

public sealed record RegisterDeviceCommand(
    Guid UserId, string DeviceName, string? DeviceType,
    string? Browser, string? OperatingSystem, string? IpAddress,
    Guid? SessionId) : IRequest<DeviceResponse>;
