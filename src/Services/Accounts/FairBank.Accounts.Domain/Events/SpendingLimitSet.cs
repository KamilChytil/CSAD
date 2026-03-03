using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record SpendingLimitSet(
    Guid AccountId,
    decimal Limit,
    Currency Currency,
    DateTime OccurredAt);
