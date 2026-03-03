using FairBank.Identity.Domain.Enums;

namespace FairBank.Identity.Application.Users.DTOs;

public sealed record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt);
