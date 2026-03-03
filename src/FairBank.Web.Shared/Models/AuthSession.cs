namespace FairBank.Web.Shared.Models;

public sealed record AuthSession(
    Guid SessionId,
    Guid UserId,
    string Token,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    DateTime ExpiresAt);
