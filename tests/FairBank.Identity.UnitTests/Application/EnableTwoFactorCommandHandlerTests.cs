using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Application.Users.Commands.EnableTwoFactor;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;

namespace FairBank.Identity.UnitTests.Application;

public class EnableTwoFactorCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITwoFactorAuthRepository _tfaRepository = Substitute.For<ITwoFactorAuthRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private User CreateUser()
    {
        return User.Create("Jan", "Novak", Email.Create("jan@example.com"),
            "hashed_password", UserRole.Client);
    }

    private static string GenerateValidTotpCode(string secret)
    {
        // Use reflection to access the private GenerateCode and Base32Decode methods
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

    [Fact]
    public async Task Handle_WithValidCode_ShouldEnableAndReturnBackupCodes()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;
        var secret = TotpHelper.GenerateSecret();
        var validCode = GenerateValidTotpCode(secret);

        var tfa = TwoFactorAuth.Create(userId, secret);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tfaRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(tfa);

        var handler = new EnableTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork);
        var command = new EnableTwoFactorCommand(userId, validCode);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(8);
        result.Should().AllSatisfy(code => code.Should().MatchRegex("^[0-9]{8}$"));

        tfa.IsEnabled.Should().BeTrue();
        user.IsTwoFactorEnabled.Should().BeTrue();

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

        var tfa = TwoFactorAuth.Create(userId, secret);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tfaRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(tfa);

        var handler = new EnableTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork);
        var command = new EnableTwoFactorCommand(userId, "000000");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid TOTP code*");
    }

    [Fact]
    public async Task Handle_WhenAlreadyEnabled_ShouldThrow()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;
        var secret = TotpHelper.GenerateSecret();

        var tfa = TwoFactorAuth.Create(userId, secret);
        tfa.Enable("[\"hashed_backup_code\"]");

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tfaRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(tfa);

        var handler = new EnableTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork);
        var command = new EnableTwoFactorCommand(userId, "123456");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already enabled*");
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

        var handler = new EnableTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork);
        var command = new EnableTwoFactorCommand(userId, "123456");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*setup not found*");
    }
}
