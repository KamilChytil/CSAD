using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Ports;
using FairBank.Identity.Application.Users.Commands.ForgotPassword;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;

namespace FairBank.Identity.UnitTests.Application;

public class ForgotPasswordCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_WithExistingUser_ShouldGenerateTokenAndSendEmail()
    {
        // Arrange
        var email = Email.Create("jan@example.com");
        var user = User.Create("Jan", "Novák", email,
            BCrypt.Net.BCrypt.HashPassword("Password1!"), UserRole.Client);

        _userRepository.GetByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new ForgotPasswordCommandHandler(_userRepository, _emailSender, _unitOfWork);
        var command = new ForgotPasswordCommand("jan@example.com");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        user.PasswordResetToken.Should().NotBeNullOrWhiteSpace();
        user.PasswordResetTokenExpiresAt.Should().NotBeNull();
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emailSender.Received(1).SendPasswordResetAsync(
            "jan@example.com",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_ShouldSilentlySucceed()
    {
        // Arrange
        _userRepository.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new ForgotPasswordCommandHandler(_userRepository, _emailSender, _unitOfWork);
        var command = new ForgotPasswordCommand("nonexistent@example.com");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert — should not throw
        await act.Should().NotThrowAsync();
        await _emailSender.DidNotReceive().SendPasswordResetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ShouldSilentlySucceed()
    {
        // Arrange
        var handler = new ForgotPasswordCommandHandler(_userRepository, _emailSender, _unitOfWork);
        var command = new ForgotPasswordCommand("not-an-email");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await _emailSender.DidNotReceive().SendPasswordResetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
