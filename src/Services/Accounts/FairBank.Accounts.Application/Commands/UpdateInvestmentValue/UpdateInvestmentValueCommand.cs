using MediatR;

namespace FairBank.Accounts.Application.Commands.UpdateInvestmentValue;

public sealed record UpdateInvestmentValueCommand(
    Guid InvestmentId,
    decimal NewPricePerUnit) : IRequest;
