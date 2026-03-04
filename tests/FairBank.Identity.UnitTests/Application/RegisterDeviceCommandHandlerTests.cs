using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.RegisterDevice;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.UnitTests.Application;

public class RegisterDeviceCommandHandlerTests
{
    private readonly IUserDeviceRepository _deviceRepository = Substitute.For<IUserDeviceRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISender _sender = Substitute.For<ISender>();

    [Fact]
    public async Task Handle_WithNewDevice_ShouldCreateDevice()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        _deviceRepository.FindByFingerprintAsync(
                userId, "Chrome", "Windows", "Desktop", Arg.Any<CancellationToken>())
            .Returns((UserDevice?)null);

        var handler = new RegisterDeviceCommandHandler(_deviceRepository, _unitOfWork, _sender);
        var command = new RegisterDeviceCommand(
            userId, "My Desktop", "Desktop", "Chrome", "Windows", "192.168.1.1", sessionId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DeviceName.Should().Be("My Desktop");
        result.DeviceType.Should().Be("Desktop");
        result.Browser.Should().Be("Chrome");
        result.OperatingSystem.Should().Be("Windows");
        result.IpAddress.Should().Be("192.168.1.1");
        result.IsCurrentDevice.Should().BeTrue();
        result.IsTrusted.Should().BeFalse();

        await _deviceRepository.Received(1).AddAsync(Arg.Any<UserDevice>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithExistingDevice_ShouldUpdateActivity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var existingDevice = UserDevice.Create(
            userId, "My Desktop", "Desktop", "Chrome", "Windows", "10.0.0.1", null);

        _deviceRepository.FindByFingerprintAsync(
                userId, "Chrome", "Windows", "Desktop", Arg.Any<CancellationToken>())
            .Returns(existingDevice);

        var handler = new RegisterDeviceCommandHandler(_deviceRepository, _unitOfWork, _sender);
        var command = new RegisterDeviceCommand(
            userId, "My Desktop", "Desktop", "Chrome", "Windows", "192.168.1.1", sessionId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IpAddress.Should().Be("192.168.1.1");
        result.IsCurrentDevice.Should().BeTrue();

        await _deviceRepository.Received(1).UpdateAsync(existingDevice, Arg.Any<CancellationToken>());
        await _deviceRepository.DidNotReceive().AddAsync(Arg.Any<UserDevice>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnDeviceResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        _deviceRepository.FindByFingerprintAsync(
                userId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserDevice?)null);

        var handler = new RegisterDeviceCommandHandler(_deviceRepository, _unitOfWork, _sender);
        var command = new RegisterDeviceCommand(
            userId, "iPhone 15", "Mobile", "Safari", "iOS", "10.0.0.5", sessionId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBe(Guid.Empty);
        result.DeviceName.Should().Be("iPhone 15");
        result.DeviceType.Should().Be("Mobile");
        result.Browser.Should().Be("Safari");
        result.OperatingSystem.Should().Be("iOS");
        result.IpAddress.Should().Be("10.0.0.5");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastActiveAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
