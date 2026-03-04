using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Application.Users.Commands.DisableTwoFactor;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.UnitTests.Application;

public class DisableTwoFactorCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITwoFactorAuthRepository _tfaRepository = Substitute.For<ITwoFactorAuthRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISender _sender = Substitute.For<ISender>();

    private User CreateUser()
    {
        return User.Create("Jan", "Novak", Email.Create("jan@example.com"),
            "hashed_password", UserRole.Client);
    }

    private static string GenerateValidTotpCode(string secret)
    {
        var base32DecodeMethod = typeof(TotpHelper).GetMethod(
            "Base32Decode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var generateCodeMethod = typeof(TotpHelper).GetMethod(
            "GenerateCode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var secretBytes = (byte[])base32DecodeMethod.Invoke(null, [secret])!;
        var timeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        return (string)generateCodeMethod.Invoke(null, [secretBytes, timeStep])!;
    }

    private static TwoFactorAuth CreateEnabledTfa(Guid userId, string secret)
    {
        var tfa = TwoFactorAuth.Create(userId, secret);
        // Generate real backup codes and hash them as the handler does
        var backupCodes = TotpHelper.GenerateBackupCodes();
        var hashedCodes = System.Text.Json.JsonSerializer.Serialize(
            backupCodes.Select(c => BCrypt.Net.BCrypt.HashPassword(c, workFactor: 10)).ToArray());
        tfa.Enable(hashedCodes);
        return tfa;
    }

    [Fact]
    public async Task Handle_WithValidCode_ShouldDisable()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;
        var secret = TotpHelper.GenerateSecret();
        var validCode = GenerateValidTotpCode(secret);

        var tfa = CreateEnabledTfa(userId, secret);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tfaRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(tfa);

        var handler = new DisableTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork, _sender);
        var command = new DisableTwoFactorCommand(userId, validCode);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        tfa.IsEnabled.Should().BeFalse();

        await _tfaRepository.Received(1).UpdateAsync(tfa, Arg.Any<CancellationToken>());
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidCode_ShouldThrow()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;
        var secret = TotpHelper.GenerateSecret();

        var tfa = CreateEnabledTfa(userId, secret);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tfaRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(tfa);

        var handler = new DisableTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork, _sender);
        var command = new DisableTwoFactorCommand(userId, "000000");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid code*");
    }

    [Fact]
    public async Task Handle_WhenNotEnabled_ShouldThrow()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;
        var secret = TotpHelper.GenerateSecret();

        var tfa = TwoFactorAuth.Create(userId, secret); // not enabled

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tfaRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(tfa);

        var handler = new DisableTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork, _sender);
        var command = new DisableTwoFactorCommand(userId, "123456");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not enabled*");
    }

    [Fact]
    public async Task Handle_WhenNoSetup_ShouldThrow()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tfaRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((TwoFactorAuth?)null);

        var handler = new DisableTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork, _sender);
        var command = new DisableTwoFactorCommand(userId, "123456");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not set up*");
    }
}
