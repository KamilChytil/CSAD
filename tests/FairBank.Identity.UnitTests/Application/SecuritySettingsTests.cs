using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.SetSecuritySettings;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;

namespace FairBank.Identity.UnitTests.Application;

public class SecuritySettingsTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private User CreateUser()
    {
        return User.Create("Jan", "Novák", Email.Create("jan@example.com"),
            "hashed_password_123", UserRole.Client);
    }

    [Fact]
    public async Task Handle_ShouldUpdateAllowInternationalPayments()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new SetSecuritySettingsCommandHandler(_userRepository, _unitOfWork);
        var command = new SetSecuritySettingsCommand(userId, false, true, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        user.AllowInternationalPayments.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldUpdateNightTransactionsEnabled()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new SetSecuritySettingsCommandHandler(_userRepository, _unitOfWork);
        var command = new SetSecuritySettingsCommand(userId, true, false, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        user.NightTransactionsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldUpdateRequireApprovalAbove()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new SetSecuritySettingsCommandHandler(_userRepository, _unitOfWork);
        var command = new SetSecuritySettingsCommand(userId, true, true, 5000m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        user.RequireApprovalAbove.Should().Be(5000m);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldThrow()
    {
        // Arrange
        _userRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new SetSecuritySettingsCommandHandler(_userRepository, _unitOfWork);
        var command = new SetSecuritySettingsCommand(Guid.NewGuid(), false, false, 1000m);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*User not found*");
    }
}
