using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Commands.SetAccountLimits;

public sealed record SetAccountLimitsCommand(
    Guid AccountId,
    decimal DailyTransactionLimit,
    decimal MonthlyTransactionLimit,
    decimal SingleTransactionLimit,
    int DailyTransactionCount,
    decimal OnlinePaymentLimit) : IRequest<AccountResponse>;
