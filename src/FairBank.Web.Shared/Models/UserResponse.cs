namespace FairBank.Web.Shared.Models;

public sealed record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    string? PersonalIdNumber = null,
    DateOnly? DateOfBirth = null,
    string? PhoneNumber = null,
    string? Street = null,
    string? City = null,
    string? ZipCode = null,
    string? Country = null,
    bool IsEmailVerified = false,
    Guid? ParentId = null);
