using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.ResetPassword;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using FairBank.SharedKernel.Logging;

namespace FairBank.Identity.UnitTests.Application;

public class ResetPasswordCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private User CreateUserWithResetToken()
    {
        var user = User.Create("Jan", "Novák", Email.Create("jan@example.com"),
            BCrypt.Net.BCrypt.HashPassword("OldPassword1!"), UserRole.Client);
        user.GeneratePasswordResetToken();
        return user;
    }

    [Fact]
    public async Task Handle_WithValidToken_ShouldResetPassword()
    {
        // Arrange
        var user = CreateUserWithResetToken();
        var token = user.PasswordResetToken!;

        _userRepository.GetByPasswordResetTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(user);

        var handler = new ResetPasswordCommandHandler(_userRepository, _unitOfWork, _auditLogger);
        var command = new ResetPasswordCommand(token, "NewPassword1!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        user.PasswordResetToken.Should().BeNull();
        user.PasswordResetTokenExpiresAt.Should().BeNull();
        BCrypt.Net.BCrypt.Verify("NewPassword1!", user.PasswordHash).Should().BeTrue();
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidToken_ShouldThrow()
    {
        // Arrange
        _userRepository.GetByPasswordResetTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new ResetPasswordCommandHandler(_userRepository, _unitOfWork, _auditLogger);
        var command = new ResetPasswordCommand("invalid-token", "NewPassword1!");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid reset token*");
    }
}
