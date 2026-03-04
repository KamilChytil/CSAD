using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record CardLimitSet(
    Guid CardId, decimal? DailyLimit, decimal? MonthlyLimit, Currency Currency, DateTime OccurredAt);
