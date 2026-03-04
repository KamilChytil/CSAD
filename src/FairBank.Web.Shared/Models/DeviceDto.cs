namespace FairBank.Web.Shared.Models;

public class DeviceDto
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = "";
    public string? DeviceType { get; set; }
    public string? Browser { get; set; }
    public string? OperatingSystem { get; set; }
    public string? IpAddress { get; set; }
    public bool IsTrusted { get; set; }
    public bool IsCurrentDevice { get; set; }
    public DateTime LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
