using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetBankers;

public sealed record GetBankersQuery() : IRequest<IEnumerable<UserResponse>>;
