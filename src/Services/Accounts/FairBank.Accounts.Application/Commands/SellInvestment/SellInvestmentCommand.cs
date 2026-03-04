using MediatR;

namespace FairBank.Accounts.Application.Commands.SellInvestment;

public sealed record SellInvestmentCommand(Guid InvestmentId) : IRequest;
