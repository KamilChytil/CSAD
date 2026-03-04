using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountsByOwner;

public sealed record GetAccountsByOwnerQuery(Guid OwnerId) : IRequest<IReadOnlyList<AccountResponse>>;
