using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.DepositMoney;

public sealed record DepositMoneyCommand(
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description) : IRequest<AccountResponse>;
