namespace FairBank.Web.Shared.Models;

public sealed record PagedUsersDto(
    IReadOnlyList<UserResponseDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record UserResponseDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt);
