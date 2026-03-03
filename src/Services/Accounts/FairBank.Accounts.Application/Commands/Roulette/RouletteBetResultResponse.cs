using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Application.Commands.Roulette;

public sealed record RouletteBetResultResponse(
    Guid AccountId,
    decimal BetAmount,
    Currency Currency,
    RouletteBetType BetType,
    int? BetNumber,
    int Spin,
    string Color,
    bool Win,
    decimal Payout,
    decimal BalanceAfter
);