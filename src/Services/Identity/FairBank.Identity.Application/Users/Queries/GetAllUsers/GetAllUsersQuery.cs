using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Enums;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetAllUsers;

public sealed record GetAllUsersQuery(
    int Page = 1, int PageSize = 20, UserRole? RoleFilter = null, string? SearchTerm = null) : IRequest<PagedUsersResponse>;
