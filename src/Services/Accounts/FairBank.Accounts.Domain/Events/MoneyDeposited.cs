using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record MoneyDeposited(
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description,
    DateTime OccurredAt);
