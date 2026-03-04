using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.LoginUser;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using FairBank.SharedKernel.Logging;
using MediatR;

namespace FairBank.Identity.UnitTests.Application;

public class LoginUserAuditTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ISender _sender = Substitute.For<ISender>();

    private User CreateUser(string password = "Password1!")
    {
        return User.Create("Jan", "Novák", Email.Create("jan@example.com"),
            BCrypt.Net.BCrypt.HashPassword(password), UserRole.Client);
    }

    [Fact]
    public async Task Handle_SuccessfulLogin_ShouldLogSuccessAuditEvent()
    {
        // Arrange
        var user = CreateUser();

        _userRepository.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new LoginUserCommandHandler(_userRepository, _unitOfWork, _auditLogger, _sender);
        var command = new LoginUserCommand("jan@example.com", "Password1!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _auditLogger.Received(1).LogSecurityEvent(
            "Login",
            "Success",
            user.Id,
            Arg.Any<string?>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task Handle_WrongPassword_ShouldLogFailedAuditEvent()
    {
        // Arrange
        var user = CreateUser();

        _userRepository.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new LoginUserCommandHandler(_userRepository, _unitOfWork, _auditLogger, _sender);
        var command = new LoginUserCommand("jan@example.com", "WrongPassword1!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _auditLogger.Received(1).LogSecurityEvent(
            "Login",
            "Failed",
            user.Id,
            "InvalidPassword",
            Arg.Any<string?>());
    }

    [Fact]
    public async Task Handle_AccountLockedAfterFailedAttempts_ShouldLogLockedAuditEvent()
    {
        // Arrange
        var user = CreateUser();

        // Simulate 4 prior failed attempts so the next one (5th) triggers lockout
        for (var i = 0; i < 4; i++)
            user.RecordFailedLogin();

        _userRepository.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new LoginUserCommandHandler(_userRepository, _unitOfWork, _auditLogger, _sender);
        var command = new LoginUserCommand("jan@example.com", "WrongPassword1!");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UserLockedOutException>();
        _auditLogger.Received(1).LogSecurityEvent(
            "Login",
            "Locked",
            user.Id,
            Arg.Is<string?>(d => d != null && d.Contains("LockedUntil")),
            Arg.Any<string?>());
    }
}
