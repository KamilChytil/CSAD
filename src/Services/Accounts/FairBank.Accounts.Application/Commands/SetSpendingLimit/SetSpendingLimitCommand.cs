using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.SetSpendingLimit;

public sealed record SetSpendingLimitCommand(
    Guid AccountId,
    decimal Limit,
    Currency Currency) : IRequest<AccountResponse>;
