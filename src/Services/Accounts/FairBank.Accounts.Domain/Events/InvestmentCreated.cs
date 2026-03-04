using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record InvestmentCreated(
    Guid InvestmentId, Guid AccountId, string Name, InvestmentType Type,
    decimal InvestedAmount, decimal Units, decimal PricePerUnit,
    Currency Currency, DateTime OccurredAt);
