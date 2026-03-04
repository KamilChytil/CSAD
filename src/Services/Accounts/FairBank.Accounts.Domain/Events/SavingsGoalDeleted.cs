namespace FairBank.Accounts.Domain.Events;

public sealed record SavingsGoalDeleted(Guid GoalId, DateTime OccurredAt);
