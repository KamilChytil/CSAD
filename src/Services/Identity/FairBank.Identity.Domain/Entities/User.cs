using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Entities;

public sealed class User : AggregateRoot<Guid>
{
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? ParentId { get; private set; }
    public User? Parent { get; private set; }

    // ── KYC Data ──────────────────────────────────────────
    public string? PersonalIdNumber { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public PhoneNumber? PhoneNumber { get; private set; }
    public Address? Address { get; private set; }
    public DateTime? AgreedToTermsAt { get; private set; }

    // ── Email Verification ──────────────────────────────
    public bool IsEmailVerified { get; private set; }
    public string? EmailVerificationToken { get; private set; }
    public DateTime? EmailVerificationTokenExpiresAt { get; private set; }

    // ── Password Reset ──────────────────────────────────
    public string? PasswordResetToken { get; private set; }
    public DateTime? PasswordResetTokenExpiresAt { get; private set; }

    // ── Two-Factor Authentication ────────────────────────
    public bool IsTwoFactorEnabled { get; private set; }

    public void EnableTwoFactor() => IsTwoFactorEnabled = true;
    public void DisableTwoFactor() => IsTwoFactorEnabled = false;

    // ── Security Settings ─────────────────────────────────
    public bool AllowInternationalPayments { get; private set; } = true;
    public bool NightTransactionsEnabled { get; private set; } = true;
    public decimal? RequireApprovalAbove { get; private set; }

    // ── Security ──────────────────────────────────────────
    /// <summary>Number of consecutive failed login attempts since last success.</summary>
    public int FailedLoginAttempts { get; private set; }
    /// <summary>When set, the account is temporarily locked until this UTC time.</summary>
    public DateTime? LockedUntil { get; private set; }
    /// <summary>The session ID of the currently active login (single-session enforcement).</summary>
    public Guid? ActiveSessionId { get; private set; }
    /// <summary>Server-enforced session expiry (set at login time). Prevents client-side tampering with ExpiresAt.</summary>
    public DateTime? SessionExpiresAt { get; private set; }

    public bool IsLockedOut => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;

    private readonly List<User> _children = [];
    public IReadOnlyCollection<User> Children => _children.AsReadOnly();

    private User() { } // EF Core

    public static User Create(
        string firstName,
        string lastName,
        Email email,
        string passwordHash,
        UserRole role,
        Guid? id = null,
        string? personalIdNumber = null,
        DateOnly? dateOfBirth = null,
        PhoneNumber? phoneNumber = null,
        Address? address = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName, nameof(firstName));
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName, nameof(lastName));
        ArgumentNullException.ThrowIfNull(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash, nameof(passwordHash));

        return new User
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            PersonalIdNumber = personalIdNumber,
            DateOfBirth = dateOfBirth,
            PhoneNumber = phoneNumber,
            Address = address
        };
    }

    public static User CreateChild(
        string firstName,
        string lastName,
        Email email,
        string passwordHash,
        Guid parentId)
    {
        var child = Create(firstName, lastName, email, passwordHash, Enums.UserRole.Child);
        child.ParentId = parentId;
        return child;
    }

    public void ChangeEmail(Email newEmail)
    {
        Email = newEmail;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeRole(UserRole newRole)
    {
        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        IsActive = false;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        IsActive = true;
        DeletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Security domain methods ───────────────────────────

    /// <summary>
    /// Called after a failed login attempt. Applies escalating time-based lockout:
    /// 5 failures → 10 min | 8 failures → 60 min | 12+ failures → 24 h.
    /// </summary>
    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        UpdatedAt = DateTime.UtcNow;

        LockedUntil = FailedLoginAttempts switch
        {
            >= 12 => DateTime.UtcNow.AddHours(24),
            >= 8  => DateTime.UtcNow.AddHours(1),
            >= 5  => DateTime.UtcNow.AddMinutes(10),
            _     => null
        };
    }

    /// <summary>
    /// Called after a successful login. Resets lockout counters and sets the
    /// new active session, invaliding any previous session (single-session).
    /// Server-enforced expiry is stored so the client cannot tamper with the token lifetime.
    /// </summary>
    public void RecordSuccessfulLogin(Guid sessionId, DateTime expiresAtUtc)
    {
        FailedLoginAttempts = 0;
        LockedUntil = null;
        ActiveSessionId = sessionId;
        SessionExpiresAt = expiresAtUtc;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Invalidates the session. Only clears if the provided sessionId matches
    /// the stored one (prevents one browser logging out another).
    /// </summary>
    public void InvalidateSession(Guid sessionId)
    {
        if (ActiveSessionId == sessionId)
        {
            ActiveSessionId = null;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Returns true if the given sessionId matches the currently active session
    /// AND the server-enforced expiry has not elapsed.
    /// </summary>
    public bool IsSessionValid(Guid sessionId)
        => ActiveSessionId.HasValue
           && ActiveSessionId.Value == sessionId
           && SessionExpiresAt.HasValue
           && SessionExpiresAt.Value > DateTime.UtcNow;

    // ── Email Verification domain methods ────────────────

    public void GenerateEmailVerificationToken()
    {
        EmailVerificationToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
    }

    public void VerifyEmail(string token)
    {
        if (IsEmailVerified)
            throw new InvalidOperationException("Email is already verified.");
        if (EmailVerificationToken != token)
            throw new InvalidOperationException("Invalid verification token.");
        if (EmailVerificationTokenExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Verification token has expired.");

        IsEmailVerified = true;
        EmailVerificationToken = null;
        EmailVerificationTokenExpiresAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Password Reset domain methods ────────────────────

    public string GeneratePasswordResetToken()
    {
        PasswordResetToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        UpdatedAt = DateTime.UtcNow;
        return PasswordResetToken;
    }

    public void ResetPassword(string token, string newPasswordHash)
    {
        if (PasswordResetToken != token)
            throw new InvalidOperationException("Invalid reset token.");
        if (PasswordResetTokenExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Reset token has expired.");

        PasswordHash = newPasswordHash;
        PasswordResetToken = null;
        PasswordResetTokenExpiresAt = null;
        ActiveSessionId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePassword(string oldPasswordVerified, string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Security Settings domain methods ─────────────────

    public void UpdateSecuritySettings(bool allowInternational, bool nightTransactions, decimal? requireApprovalAbove)
    {
        AllowInternationalPayments = allowInternational;
        NightTransactionsEnabled = nightTransactions;
        RequireApprovalAbove = requireApprovalAbove;
        UpdatedAt = DateTime.UtcNow;
    }
}
