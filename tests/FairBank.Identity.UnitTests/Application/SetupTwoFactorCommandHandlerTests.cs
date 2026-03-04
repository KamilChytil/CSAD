using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.SetupTwoFactor;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.UnitTests.Application;

public class SetupTwoFactorCommandHandlerTests
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

    [Fact]
    public async Task Handle_WithValidUser_ShouldReturnSetupResponse()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tfaRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((TwoFactorAuth?)null);

        var handler = new SetupTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork, _sender);
        var command = new SetupTwoFactorCommand(userId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Secret.Should().NotBeNullOrWhiteSpace();
        result.OtpAuthUri.Should().NotBeNullOrWhiteSpace();
        result.OtpAuthUri.Should().Contain("otpauth://totp/");
        result.OtpAuthUri.Should().Contain(result.Secret);
        result.IsAlreadyEnabled.Should().BeFalse();

        await _tfaRepository.Received(1).AddAsync(Arg.Any<TwoFactorAuth>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithAlreadyEnabledTfa_ShouldReturnIsAlreadyEnabled()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        var existingTfa = TwoFactorAuth.Create(userId, "EXISTING_SECRET");
        existingTfa.Enable("[\"hashed_backup_code\"]");

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tfaRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(existingTfa);

        var handler = new SetupTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork, _sender);
        var command = new SetupTwoFactorCommand(userId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Secret.Should().BeEmpty();
        result.OtpAuthUri.Should().BeEmpty();
        result.IsAlreadyEnabled.Should().BeTrue();

        await _tfaRepository.DidNotReceive().AddAsync(Arg.Any<TwoFactorAuth>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithExistingNonEnabledTfa_ShouldDeleteAndRecreate()
    {
        // Arrange
        var user = CreateUser();
        var userId = user.Id;

        var existingTfa = TwoFactorAuth.Create(userId, "OLD_SECRET");

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _tfaRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(existingTfa);

        var handler = new SetupTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork, _sender);
        var command = new SetupTwoFactorCommand(userId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsAlreadyEnabled.Should().BeFalse();
        result.Secret.Should().NotBeNullOrWhiteSpace();

        await _tfaRepository.Received(1).DeleteByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await _tfaRepository.Received(1).AddAsync(Arg.Any<TwoFactorAuth>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldThrow()
    {
        // Arrange
        _userRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new SetupTwoFactorCommandHandler(_userRepository, _tfaRepository, _unitOfWork, _sender);
        var command = new SetupTwoFactorCommand(Guid.NewGuid());

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*User not found*");
    }
}
