namespace FairBank.Accounts.Domain.Events;

public sealed record TransactionRejected(
    Guid TransactionId,
    Guid ApproverId,
    string Reason,
    DateTime OccurredAt);
