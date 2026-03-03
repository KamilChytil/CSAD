using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.WithdrawMoney;

public sealed record WithdrawMoneyCommand(
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description) : IRequest<AccountResponse>;
