namespace FairBank.Accounts.Domain.Events;

public sealed record AccountClosed(
    Guid AccountId,
    DateTime OccurredAt);
