namespace FairBank.Payments.Application.Services;

public static class LimitEnforcementService
{
    public static void EnforceSingleTransactionLimit(decimal amount, decimal? limit)
    {
        if (limit.HasValue && amount > limit.Value)
            throw new InvalidOperationException(
                $"Platba {amount} překračuje limit na jednu transakci ({limit.Value}).");
    }

    public static void EnforceDailyLimit(decimal todayTotal, decimal amount, decimal? dailyLimit)
    {
        if (dailyLimit.HasValue && (todayTotal + amount) > dailyLimit.Value)
            throw new InvalidOperationException(
                $"Platba překračuje denní limit. Dnes utraceno: {todayTotal}, limit: {dailyLimit.Value}.");
    }

    public static void EnforceMonthlyLimit(decimal monthTotal, decimal amount, decimal? monthlyLimit)
    {
        if (monthlyLimit.HasValue && (monthTotal + amount) > monthlyLimit.Value)
            throw new InvalidOperationException(
                $"Platba překračuje měsíční limit. Tento měsíc utraceno: {monthTotal}, limit: {monthlyLimit.Value}.");
    }

    public static void EnforceDailyCount(int todayCount, int? maxCount)
    {
        if (maxCount.HasValue && todayCount >= maxCount.Value)
            throw new InvalidOperationException(
                $"Překročen maximální počet plateb za den ({maxCount.Value}).");
    }

    public static void EnforceNightRestriction(bool nightEnabled)
    {
        if (!nightEnabled)
        {
            var hour = DateTime.UtcNow.Hour;
            if (hour >= 23 || hour < 6)
                throw new InvalidOperationException(
                    "Noční platby (23:00–06:00) jsou zakázány v bezpečnostním nastavení.");
        }
    }
}
