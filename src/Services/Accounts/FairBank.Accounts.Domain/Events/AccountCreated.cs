using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record AccountCreated(
    Guid AccountId,
    Guid OwnerId,
    string AccountNumber,
    Currency Currency,
    DateTime OccurredAt);
