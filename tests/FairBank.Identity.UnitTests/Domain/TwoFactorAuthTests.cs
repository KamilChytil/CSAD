using FluentAssertions;
using FairBank.Identity.Domain.Entities;

namespace FairBank.Identity.UnitTests.Domain;

public class TwoFactorAuthTests
{
    [Fact]
    public void Create_ShouldSetDefaultValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var secret = "JBSWY3DPEHPK3PXP";

        // Act
        var tfa = TwoFactorAuth.Create(userId, secret);

        // Assert
        tfa.Id.Should().NotBe(Guid.Empty);
        tfa.UserId.Should().Be(userId);
        tfa.SecretKey.Should().Be(secret);
        tfa.IsEnabled.Should().BeFalse();
        tfa.BackupCodes.Should().BeNull();
        tfa.EnabledAt.Should().BeNull();
        tfa.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Enable_ShouldSetIsEnabledTrue()
    {
        // Arrange
        var tfa = TwoFactorAuth.Create(Guid.NewGuid(), "SECRET");
        var hashedBackupCodes = "[\"hashed_code_1\",\"hashed_code_2\"]";

        // Act
        tfa.Enable(hashedBackupCodes);

        // Assert
        tfa.IsEnabled.Should().BeTrue();
        tfa.EnabledAt.Should().NotBeNull();
        tfa.EnabledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        tfa.BackupCodes.Should().Be(hashedBackupCodes);
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_ShouldThrow()
    {
        // Arrange
        var tfa = TwoFactorAuth.Create(Guid.NewGuid(), "SECRET");
        tfa.Enable("[\"code\"]");

        // Act
        var act = () => tfa.Enable("[\"another_code\"]");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already enabled*");
    }

    [Fact]
    public void Disable_ShouldSetIsEnabledFalse()
    {
        // Arrange
        var tfa = TwoFactorAuth.Create(Guid.NewGuid(), "SECRET");
        tfa.Enable("[\"code\"]");

        // Act
        tfa.Disable();

        // Assert
        tfa.IsEnabled.Should().BeFalse();
        tfa.EnabledAt.Should().BeNull();
        tfa.BackupCodes.Should().BeNull();
    }

    [Fact]
    public void Disable_WhenNotEnabled_ShouldThrow()
    {
        // Arrange
        var tfa = TwoFactorAuth.Create(Guid.NewGuid(), "SECRET");

        // Act
        var act = () => tfa.Disable();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not enabled*");
    }

    [Fact]
    public void RegenerateBackupCodes_WhenEnabled_ShouldUpdate()
    {
        // Arrange
        var tfa = TwoFactorAuth.Create(Guid.NewGuid(), "SECRET");
        tfa.Enable("[\"old_code\"]");
        var newHashedCodes = "[\"new_code_1\",\"new_code_2\"]";

        // Act
        tfa.RegenerateBackupCodes(newHashedCodes);

        // Assert
        tfa.BackupCodes.Should().Be(newHashedCodes);
    }

    [Fact]
    public void RegenerateBackupCodes_WhenNotEnabled_ShouldThrow()
    {
        // Arrange
        var tfa = TwoFactorAuth.Create(Guid.NewGuid(), "SECRET");

        // Act
        var act = () => tfa.RegenerateBackupCodes("[\"code\"]");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be enabled*");
    }
}
