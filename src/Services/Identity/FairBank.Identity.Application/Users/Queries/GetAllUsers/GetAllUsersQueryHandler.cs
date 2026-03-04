using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetAllUsers;

public sealed class GetAllUsersQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetAllUsersQuery, PagedUsersResponse>
{
    public async Task<PagedUsersResponse> Handle(GetAllUsersQuery request, CancellationToken ct)
    {
        var allUsers = await userRepository.GetAllAsync(ct);

        var filtered = allUsers.AsEnumerable();

        if (request.RoleFilter.HasValue)
            filtered = filtered.Where(u => u.Role == request.RoleFilter.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLowerInvariant();
            filtered = filtered.Where(u =>
                u.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Value.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var materialized = filtered.ToList();
        var totalCount = materialized.Count;

        var items = materialized
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserResponse(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email.Value,
                u.Role,
                u.IsActive,
                u.CreatedAt,
                u.PersonalIdNumber,
                u.DateOfBirth,
                u.PhoneNumber?.Value,
                u.Address?.Street,
                u.Address?.City,
                u.Address?.ZipCode,
                u.Address?.Country,
                u.IsEmailVerified))
            .ToList();

        return new PagedUsersResponse(items, totalCount, request.Page, request.PageSize);
    }
}
