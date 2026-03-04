using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.RestoreUser;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;

namespace FairBank.Identity.UnitTests.Application;

public class RestoreUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private User CreateDeletedUser()
    {
        var user = User.Create("Jan", "Novák", Email.Create("jan@example.com"),
            "hashed_password_123", UserRole.Client);
        user.SoftDelete();
        return user;
    }

    [Fact]
    public async Task Handle_WithDeletedUser_ShouldRestoreSuccessfully()
    {
        // Arrange
        var user = CreateDeletedUser();
        var userId = user.Id;

        _userRepository.GetDeletedByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new RestoreUserCommandHandler(_userRepository, _unitOfWork);
        var command = new RestoreUserCommand(userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        user.IsDeleted.Should().BeFalse();
        user.IsActive.Should().BeTrue();
        user.DeletedAt.Should().BeNull();
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldThrow()
    {
        // Arrange
        _userRepository.GetDeletedByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new RestoreUserCommandHandler(_userRepository, _unitOfWork);
        var command = new RestoreUserCommand(Guid.NewGuid());

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Deleted user not found*");
    }

    [Fact]
    public async Task Handle_ShouldCallSaveChangesOnUnitOfWork()
    {
        // Arrange
        var user = CreateDeletedUser();
        var userId = user.Id;

        _userRepository.GetDeletedByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new RestoreUserCommandHandler(_userRepository, _unitOfWork);
        var command = new RestoreUserCommand(userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
