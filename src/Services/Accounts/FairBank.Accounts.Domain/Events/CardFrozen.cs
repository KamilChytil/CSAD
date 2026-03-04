namespace FairBank.Accounts.Domain.Events;

public sealed record CardFrozen(Guid CardId, DateTime OccurredAt);
