using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record SavingsGoalCreated(
    Guid GoalId, Guid AccountId, string Name, string? Description,
    decimal TargetAmount, Currency Currency, DateTime OccurredAt);
