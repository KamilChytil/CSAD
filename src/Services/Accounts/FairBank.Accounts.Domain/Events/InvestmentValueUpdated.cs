namespace FairBank.Accounts.Domain.Events;

public sealed record InvestmentValueUpdated(
    Guid InvestmentId, decimal NewPricePerUnit, DateTime OccurredAt);
