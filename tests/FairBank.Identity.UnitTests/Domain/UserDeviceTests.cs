using FluentAssertions;
using FairBank.Identity.Domain.Entities;

namespace FairBank.Identity.UnitTests.Domain;

public class UserDeviceTests
{
    [Fact]
    public void Create_ShouldSetDefaultValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var device = UserDevice.Create(
            userId, "My Desktop", "Desktop", "Chrome", "Windows", "192.168.1.1", sessionId);

        // Assert
        device.Id.Should().NotBe(Guid.Empty);
        device.UserId.Should().Be(userId);
        device.DeviceName.Should().Be("My Desktop");
        device.DeviceType.Should().Be("Desktop");
        device.Browser.Should().Be("Chrome");
        device.OperatingSystem.Should().Be("Windows");
        device.IpAddress.Should().Be("192.168.1.1");
        device.SessionId.Should().Be(sessionId);
        device.IsTrusted.Should().BeFalse();
        device.IsCurrentDevice.Should().BeTrue();
        device.LastActiveAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        device.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateActivity_ShouldUpdateFields()
    {
        // Arrange
        var device = UserDevice.Create(
            Guid.NewGuid(), "Desktop", "Desktop", "Chrome", "Windows", "10.0.0.1", null);
        var newSessionId = Guid.NewGuid();

        // Act
        device.UpdateActivity("192.168.1.100", newSessionId);

        // Assert
        device.IpAddress.Should().Be("192.168.1.100");
        device.SessionId.Should().Be(newSessionId);
        device.IsCurrentDevice.Should().BeTrue();
        device.LastActiveAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkTrusted_ShouldSetIsTrustedTrue()
    {
        // Arrange
        var device = UserDevice.Create(
            Guid.NewGuid(), "Desktop", "Desktop", "Chrome", "Windows", "10.0.0.1", null);

        // Act
        device.MarkTrusted();

        // Assert
        device.IsTrusted.Should().BeTrue();
    }

    [Fact]
    public void UnmarkTrusted_ShouldSetIsTrustedFalse()
    {
        // Arrange
        var device = UserDevice.Create(
            Guid.NewGuid(), "Desktop", "Desktop", "Chrome", "Windows", "10.0.0.1", null);
        device.MarkTrusted();

        // Act
        device.UnmarkTrusted();

        // Assert
        device.IsTrusted.Should().BeFalse();
    }

    [Fact]
    public void Revoke_ShouldClearSessionAndCurrentDevice()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var device = UserDevice.Create(
            Guid.NewGuid(), "Desktop", "Desktop", "Chrome", "Windows", "10.0.0.1", sessionId);

        // Act
        device.Revoke();

        // Assert
        device.SessionId.Should().BeNull();
        device.IsCurrentDevice.Should().BeFalse();
    }
}
