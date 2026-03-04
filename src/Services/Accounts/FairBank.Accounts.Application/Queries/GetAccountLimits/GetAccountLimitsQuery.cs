using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountLimits;

public sealed record GetAccountLimitsQuery(Guid AccountId) : IRequest<AccountLimitsResponse?>;
