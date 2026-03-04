namespace FairBank.Accounts.Application.DTOs;

public sealed record AccountLimitsResponse(
    decimal DailyTransactionLimit,
    decimal MonthlyTransactionLimit,
    decimal SingleTransactionLimit,
    int DailyTransactionCount,
    decimal OnlinePaymentLimit,
    decimal DailyUsed,
    decimal MonthlyUsed,
    int DailyCountUsed);
