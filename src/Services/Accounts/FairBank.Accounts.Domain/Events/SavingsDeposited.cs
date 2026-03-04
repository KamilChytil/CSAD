using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record SavingsDeposited(
    Guid GoalId, decimal Amount, Currency Currency, DateTime OccurredAt);
