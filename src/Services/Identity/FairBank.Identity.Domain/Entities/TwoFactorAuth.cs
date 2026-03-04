using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Entities;

public sealed class TwoFactorAuth : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string SecretKey { get; private set; } = null!;
    public bool IsEnabled { get; private set; }
    public string? BackupCodes { get; private set; } // JSON array of hashed codes
    public DateTime CreatedAt { get; private set; }
    public DateTime? EnabledAt { get; private set; }

    private TwoFactorAuth() { } // EF Core

    public static TwoFactorAuth Create(Guid userId, string secretKey)
    {
        return new TwoFactorAuth
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SecretKey = secretKey,
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Enable(string hashedBackupCodes)
    {
        if (IsEnabled)
            throw new InvalidOperationException("2FA is already enabled.");

        IsEnabled = true;
        EnabledAt = DateTime.UtcNow;
        BackupCodes = hashedBackupCodes;
    }

    public void Disable()
    {
        if (!IsEnabled)
            throw new InvalidOperationException("2FA is not enabled.");

        IsEnabled = false;
        EnabledAt = null;
        BackupCodes = null;
    }

    public void RegenerateBackupCodes(string hashedBackupCodes)
    {
        if (!IsEnabled)
            throw new InvalidOperationException("2FA must be enabled to regenerate backup codes.");

        BackupCodes = hashedBackupCodes;
    }
}
