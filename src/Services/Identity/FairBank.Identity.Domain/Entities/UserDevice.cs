using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Entities;

public sealed class UserDevice : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string DeviceName { get; private set; } = null!;
    public string? DeviceType { get; private set; } // Desktop, Mobile, Tablet
    public string? Browser { get; private set; }
    public string? OperatingSystem { get; private set; }
    public string? IpAddress { get; private set; }
    public Guid? SessionId { get; private set; }
    public bool IsTrusted { get; private set; }
    public bool IsCurrentDevice { get; private set; }
    public DateTime LastActiveAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private UserDevice() { }

    public static UserDevice Create(
        Guid userId, string deviceName, string? deviceType,
        string? browser, string? operatingSystem, string? ipAddress,
        Guid? sessionId)
    {
        return new UserDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceName = deviceName,
            DeviceType = deviceType,
            Browser = browser,
            OperatingSystem = operatingSystem,
            IpAddress = ipAddress,
            SessionId = sessionId,
            IsTrusted = false,
            IsCurrentDevice = true,
            LastActiveAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateActivity(string? ipAddress, Guid? sessionId)
    {
        IpAddress = ipAddress;
        SessionId = sessionId;
        LastActiveAt = DateTime.UtcNow;
        IsCurrentDevice = true;
    }

    public void MarkTrusted() => IsTrusted = true;
    public void UnmarkTrusted() => IsTrusted = false;

    public void Revoke()
    {
        SessionId = null;
        IsCurrentDevice = false;
    }
}
