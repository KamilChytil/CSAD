namespace FairBank.Accounts.Domain.Events;

public sealed record TransactionApproved(
    Guid TransactionId,
    Guid ApproverId,
    DateTime OccurredAt);
