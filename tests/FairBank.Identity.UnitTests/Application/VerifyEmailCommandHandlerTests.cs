using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.VerifyEmail;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.UnitTests.Application;

public class VerifyEmailCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISender _sender = Substitute.For<ISender>();

    private User CreateUserWithVerificationToken()
    {
        var user = User.Create("Jan", "Novák", Email.Create("jan@example.com"),
            BCrypt.Net.BCrypt.HashPassword("Password1!"), UserRole.Client);
        user.GenerateEmailVerificationToken();
        return user;
    }

    [Fact]
    public async Task Handle_WithValidToken_ShouldVerifyEmail()
    {
        // Arrange
        var user = CreateUserWithVerificationToken();
        var token = user.EmailVerificationToken!;

        _userRepository.GetByEmailVerificationTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new VerifyEmailCommandHandler(_userRepository, _unitOfWork, _sender);
        var command = new VerifyEmailCommand(token);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        user.IsEmailVerified.Should().BeTrue();
        user.EmailVerificationToken.Should().BeNull();
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidToken_ShouldThrow()
    {
        // Arrange
        _userRepository.GetByEmailVerificationTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new VerifyEmailCommandHandler(_userRepository, _unitOfWork, _sender);
        var command = new VerifyEmailCommand("invalid-token");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid verification token*");
    }
}
