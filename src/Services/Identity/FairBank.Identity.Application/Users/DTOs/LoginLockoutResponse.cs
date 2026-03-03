namespace FairBank.Identity.Application.Users.DTOs;

/// <summary>Returned with HTTP 429 when a login attempt hits a locked account.</summary>
public sealed record LoginLockoutResponse(
    bool IsLockedOut,
    DateTime LockedUntil,
    int RemainingSeconds);
