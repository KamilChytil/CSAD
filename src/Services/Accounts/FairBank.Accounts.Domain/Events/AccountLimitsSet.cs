namespace FairBank.Accounts.Domain.Events;

public sealed record AccountLimitsSet(
    Guid AccountId,
    decimal DailyTransactionLimit,
    decimal MonthlyTransactionLimit,
    decimal SingleTransactionLimit,
    int DailyTransactionCount,
    decimal OnlinePaymentLimit,
    DateTime OccurredAt);
