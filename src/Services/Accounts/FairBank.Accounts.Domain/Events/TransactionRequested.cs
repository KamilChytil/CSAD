using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record TransactionRequested(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description,
    Guid RequestedBy,
    DateTime OccurredAt);
