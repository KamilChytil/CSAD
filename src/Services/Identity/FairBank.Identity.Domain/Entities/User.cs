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
        Guid? id = null)
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
            CreatedAt = DateTime.UtcNow
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
}
