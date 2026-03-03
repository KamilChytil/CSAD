using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<UserResponse?>;
