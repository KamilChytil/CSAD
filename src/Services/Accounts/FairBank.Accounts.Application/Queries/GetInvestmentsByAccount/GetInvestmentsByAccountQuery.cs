using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetInvestmentsByAccount;

public sealed record GetInvestmentsByAccountQuery(Guid AccountId) : IRequest<IReadOnlyList<InvestmentResponse>>;
