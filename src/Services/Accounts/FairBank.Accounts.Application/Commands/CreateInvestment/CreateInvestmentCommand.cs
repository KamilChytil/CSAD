using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreateInvestment;

public sealed record CreateInvestmentCommand(
    Guid AccountId,
    string Name,
    InvestmentType Type,
    decimal Amount,
    decimal Units,
    decimal PricePerUnit,
    Currency Currency) : IRequest<InvestmentResponse>;
