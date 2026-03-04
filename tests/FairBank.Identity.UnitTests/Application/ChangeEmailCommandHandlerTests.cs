using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.ChangeEmail;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;

namespace FairBank.Identity.UnitTests.Application;

public class ChangeEmailCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private User CreateUser(string email = "jan@example.com")
    {
        return User.Create("Jan", "Novak", Email.Create(email),
            BCrypt.Net.BCrypt.HashPassword("Password1!"), UserRole.Client);
    }

    [Fact]
    public async Task Handle_WithValidEmail_ShouldChangeEmail()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _userRepository.ExistsWithEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new ChangeEmailCommandHandler(_userRepository, _unitOfWork);
        var command = new ChangeEmailCommand(userId, "new@example.com");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        user.Email.Value.Should().Be("new@example.com");
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldThrow()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _userRepository.ExistsWithEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new ChangeEmailCommandHandler(_userRepository, _unitOfWork);
        var command = new ChangeEmailCommand(userId, "existing@example.com");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldThrow()
    {
        // Arrange
        _userRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new ChangeEmailCommandHandler(_userRepository, _unitOfWork);
        var command = new ChangeEmailCommand(Guid.NewGuid(), "new@example.com");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*User not found*");
    }
}
