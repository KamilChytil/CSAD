using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Ports;
using FairBank.Identity.Application.Users.Commands.ForgotPassword;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.UnitTests.Application;

public class ForgotPasswordCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISender _sender = Substitute.For<ISender>();

    [Fact]
    public async Task Handle_WithExistingUser_ShouldGenerateTokenAndSendEmail()
    {
        var email = Email.Create("jan@example.com");
        var user = User.Create("Jan", "Novák", email,
            BCrypt.Net.BCrypt.HashPassword("Password1!"), UserRole.Client);

        _userRepository.GetByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new ForgotPasswordCommandHandler(_userRepository, _emailSender, _unitOfWork, _sender);
        var command = new ForgotPasswordCommand("jan@example.com");

        await handler.Handle(command, CancellationToken.None);

        user.PasswordResetToken.Should().NotBeNullOrWhiteSpace();
        user.PasswordResetTokenExpiresAt.Should().NotBeNull();
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emailSender.Received(1).SendPasswordResetAsync(
            "jan@example.com", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_ShouldSilentlySucceed()
    {
        _userRepository.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new ForgotPasswordCommandHandler(_userRepository, _emailSender, _unitOfWork, _sender);
        var command = new ForgotPasswordCommand("nonexistent@example.com");

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _emailSender.DidNotReceive().SendPasswordResetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ShouldSilentlySucceed()
    {
        var handler = new ForgotPasswordCommandHandler(_userRepository, _emailSender, _unitOfWork, _sender);
        var command = new ForgotPasswordCommand("not-an-email");

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _emailSender.DidNotReceive().SendPasswordResetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
