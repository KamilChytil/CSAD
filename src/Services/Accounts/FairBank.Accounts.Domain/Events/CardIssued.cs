using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record CardIssued(
    Guid CardId, Guid AccountId, string CardNumber, string HolderName,
    DateTime ExpirationDate, CardType Type, DateTime OccurredAt);
