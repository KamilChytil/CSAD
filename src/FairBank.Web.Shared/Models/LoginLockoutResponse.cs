namespace FairBank.Web.Shared.Models;

/// <summary>Deserialized from HTTP 429 response body when login rate-limit is hit.</summary>
public sealed record LoginLockoutResponse(
    bool IsLockedOut,
    DateTime LockedUntil,
    int RemainingSeconds);
