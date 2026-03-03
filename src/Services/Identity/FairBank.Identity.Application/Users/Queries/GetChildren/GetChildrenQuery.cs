using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetChildren;

public sealed record GetChildrenQuery(Guid ParentId) : IRequest<IReadOnlyList<UserResponse>>;
