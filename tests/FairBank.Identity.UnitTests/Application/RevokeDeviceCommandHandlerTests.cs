using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.RevokeDevice;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.UnitTests.Application;

public class RevokeDeviceCommandHandlerTests
{
    private readonly IUserDeviceRepository _deviceRepository = Substitute.For<IUserDeviceRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISender _sender = Substitute.For<ISender>();

    [Fact]
    public async Task Handle_WithValidDevice_ShouldRevoke()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var device = UserDevice.Create(userId, "Desktop", "Desktop", "Chrome", "Windows", "10.0.0.1", sessionId);
        var deviceId = device.Id;

        var user = User.Create("Jan", "Novak", Email.Create("jan@example.com"),
            "hashed_password", UserRole.Client, id: userId);

        _deviceRepository.GetByIdAsync(deviceId, Arg.Any<CancellationToken>())
            .Returns(device);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new RevokeDeviceCommandHandler(_deviceRepository, _userRepository, _unitOfWork, _sender);
        var command = new RevokeDeviceCommand(deviceId, userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        device.SessionId.Should().BeNull();
        device.IsCurrentDevice.Should().BeFalse();

        await _deviceRepository.Received(1).UpdateAsync(device, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentDevice_ShouldThrow()
    {
        // Arrange
        _deviceRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((UserDevice?)null);

        var handler = new RevokeDeviceCommandHandler(_deviceRepository, _userRepository, _unitOfWork, _sender);
        var command = new RevokeDeviceCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Device not found*");
    }

    [Fact]
    public async Task Handle_WithWrongUser_ShouldThrow()
    {
        // Arrange
        var deviceOwnerUserId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var device = UserDevice.Create(deviceOwnerUserId, "Desktop", "Desktop", "Chrome", "Windows", "10.0.0.1", null);
        var deviceId = device.Id;

        _deviceRepository.GetByIdAsync(deviceId, Arg.Any<CancellationToken>())
            .Returns(device);

        var handler = new RevokeDeviceCommandHandler(_deviceRepository, _userRepository, _unitOfWork, _sender);
        var command = new RevokeDeviceCommand(deviceId, differentUserId);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not belong to user*");
    }
}
