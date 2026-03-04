using FairBank.SharedKernel.Domain;

namespace FairBank.Accounts.Domain.ValueObjects;

public sealed class AccountLimits : ValueObject
{
    public decimal DailyTransactionLimit { get; }
    public decimal MonthlyTransactionLimit { get; }
    public decimal SingleTransactionLimit { get; }
    public int DailyTransactionCount { get; }
    public decimal OnlinePaymentLimit { get; }

    private AccountLimits(decimal daily, decimal monthly, decimal single, int count, decimal online)
    {
        DailyTransactionLimit = daily;
        MonthlyTransactionLimit = monthly;
        SingleTransactionLimit = single;
        DailyTransactionCount = count;
        OnlinePaymentLimit = online;
    }

    public static AccountLimits Create(
        decimal dailyLimit = 100000,
        decimal monthlyLimit = 500000,
        decimal singleLimit = 50000,
        int dailyCount = 50,
        decimal onlineLimit = 30000)
    {
        if (dailyLimit < 0 || monthlyLimit < 0 || singleLimit < 0 || dailyCount < 0 || onlineLimit < 0)
            throw new ArgumentException("Limits must be non-negative.");
        if (dailyLimit > monthlyLimit)
            throw new ArgumentException("Daily limit cannot exceed monthly limit.");

        return new AccountLimits(dailyLimit, monthlyLimit, singleLimit, dailyCount, onlineLimit);
    }

    public static AccountLimits Default() => Create();

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return DailyTransactionLimit;
        yield return MonthlyTransactionLimit;
        yield return SingleTransactionLimit;
        yield return DailyTransactionCount;
        yield return OnlinePaymentLimit;
    }
}
