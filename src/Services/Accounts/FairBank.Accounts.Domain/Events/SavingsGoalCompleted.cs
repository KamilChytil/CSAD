namespace FairBank.Accounts.Domain.Events;

public sealed record SavingsGoalCompleted(Guid GoalId, DateTime OccurredAt);
