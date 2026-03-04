using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetInvestmentById;

public sealed record GetInvestmentByIdQuery(Guid InvestmentId) : IRequest<InvestmentResponse?>;
