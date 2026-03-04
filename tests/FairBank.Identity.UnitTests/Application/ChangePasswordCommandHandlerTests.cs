using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.ChangePassword;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;

namespace FairBank.Identity.UnitTests.Application;

public class ChangePasswordCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private User CreateUser(string password = "OldPassword1!")
    {
        return User.Create("Jan", "Novák", Email.Create("jan@example.com"),
            BCrypt.Net.BCrypt.HashPassword(password), UserRole.Client);
    }

    [Fact]
    public async Task Handle_WithCorrectCurrentPassword_ShouldChangePassword()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new ChangePasswordCommandHandler(_userRepository, _unitOfWork);
        var command = new ChangePasswordCommand(userId, "OldPassword1!", "NewPassword1!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("NewPassword1!", user.PasswordHash).Should().BeTrue();
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithIncorrectCurrentPassword_ShouldThrow()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new ChangePasswordCommandHandler(_userRepository, _unitOfWork);
        var command = new ChangePasswordCommand(userId, "WrongPassword1!", "NewPassword1!");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Current password is incorrect*");
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldThrow()
    {
        // Arrange
        _userRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new ChangePasswordCommandHandler(_userRepository, _unitOfWork);
        var command = new ChangePasswordCommand(Guid.NewGuid(), "OldPassword1!", "NewPassword1!");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*User not found*");
    }
}
