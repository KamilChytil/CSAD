namespace FairBank.Accounts.Domain.Events;

public sealed record CardDeactivated(Guid CardId, DateTime OccurredAt);
