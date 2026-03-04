namespace FairBank.Identity.Application.Users.DTOs;

public sealed record PagedUsersResponse(
    IReadOnlyList<UserResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
