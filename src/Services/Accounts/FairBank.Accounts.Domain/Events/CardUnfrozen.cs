namespace FairBank.Accounts.Domain.Events;

public sealed record CardUnfrozen(Guid CardId, DateTime OccurredAt);
