namespace FairBank.Identity.Domain.Entities;

/// <summary>
/// Thrown when a login attempt is made against a temporarily locked account.
/// Carries the UTC time at which the lockout expires.
/// </summary>
public sealed class UserLockedOutException(DateTime lockedUntil)
    : Exception($"Account is locked until {lockedUntil:O}.")
{
    public DateTime LockedUntil { get; } = lockedUntil;
}
