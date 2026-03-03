namespace FairBank.Web.Shared.Models;

public sealed record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt);
