namespace FairBank.Identity.Application.Users.DTOs;

public sealed record DeviceResponse(
    Guid Id,
    string DeviceName,
    string? DeviceType,
    string? Browser,
    string? OperatingSystem,
    string? IpAddress,
    bool IsTrusted,
    bool IsCurrentDevice,
    DateTime LastActiveAt,
    DateTime CreatedAt);
