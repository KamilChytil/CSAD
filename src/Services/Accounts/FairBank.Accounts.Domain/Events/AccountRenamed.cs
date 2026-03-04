namespace FairBank.Accounts.Domain.Events;

public sealed record AccountRenamed(
    Guid AccountId,
    string? Alias,
    DateTime OccurredAt);
