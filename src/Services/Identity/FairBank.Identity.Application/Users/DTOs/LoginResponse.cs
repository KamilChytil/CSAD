namespace FairBank.Identity.Application.Users.DTOs;

public sealed record LoginResponse(
    string Token,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    Guid SessionId,
    DateTime ExpiresAt);
