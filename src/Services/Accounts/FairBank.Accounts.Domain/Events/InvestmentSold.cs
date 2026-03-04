using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record InvestmentSold(
    Guid InvestmentId, decimal SoldAmount, Currency Currency, DateTime OccurredAt);
