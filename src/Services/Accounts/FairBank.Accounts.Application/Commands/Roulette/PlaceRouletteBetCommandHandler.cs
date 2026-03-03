using System.Security.Cryptography;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.Roulette;

public sealed class PlaceRouletteBetCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<PlaceRouletteBetCommand, RouletteBetResultResponse>
{
    private static readonly HashSet<int> RedNumbers =
    [
        1,3,5,7,9,12,14,16,18,19,21,23,25,27,30,32,34,36
    ];

    public async Task<RouletteBetResultResponse> Handle(PlaceRouletteBetCommand request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct)
                      ?? throw new InvalidOperationException($"Account {request.AccountId} not found.");

        if (request.Amount <= 0)
            throw new InvalidOperationException("Bet amount must be positive.");

        if (request.BetType == RouletteBetType.Straight)
        {
            if (request.Number is null or < 0 or > 36)
                throw new InvalidOperationException("Straight bet requires Number in range 0..36.");
        }

        // Stake leaves the account immediately (auditable, prevents “free retries”)
        account.Withdraw(
            Money.Create(request.Amount, request.Currency),
            $"Roulette bet: {request.BetType}" + (request.Number is not null ? $" #{request.Number}" : string.Empty)
        );

        var spin = RandomNumberGenerator.GetInt32(0, 37);
        var color = spin == 0 ? "Green" : (RedNumbers.Contains(spin) ? "Red" : "Black");

        var win = IsWin(request.BetType, spin, color, request.Number);
        var payout = win ? CalculatePayout(request.BetType, request.Amount) : 0m;

        if (payout > 0)
        {
            account.Deposit(
                Money.Create(payout, request.Currency),
                $"Roulette win: spin {spin} ({color})"
            );
        }

        await eventStore.AppendEventsAsync(account, ct);

        return new RouletteBetResultResponse(
            AccountId: account.Id,
            BetAmount: request.Amount,
            Currency: request.Currency,
            BetType: request.BetType,
            BetNumber: request.Number,
            Spin: spin,
            Color: color,
            Win: win,
            Payout: payout,
            BalanceAfter: account.Balance.Amount
        );
    }

    private static bool IsWin(RouletteBetType betType, int spin, string color, int? number) =>
        betType switch
        {
            RouletteBetType.Straight => spin == number,
            RouletteBetType.Red => spin != 0 && color == "Red",
            RouletteBetType.Black => spin != 0 && color == "Black",
            RouletteBetType.Even => spin != 0 && spin % 2 == 0,
            RouletteBetType.Odd => spin != 0 && spin % 2 == 1,
            RouletteBetType.Low => spin is >= 1 and <= 18,
            RouletteBetType.High => spin is >= 19 and <= 36,
            _ => false
        };

    // payout includes returning the stake for wins.
    // Since we already withdrew the stake, depositing payout=bet*2 (even money) nets +bet.
    private static decimal CalculatePayout(RouletteBetType betType, decimal betAmount) =>
        betType switch
        {
            RouletteBetType.Straight => betAmount * 36m, // 35:1 + stake
            RouletteBetType.Red or RouletteBetType.Black
                or RouletteBetType.Even or RouletteBetType.Odd
                or RouletteBetType.Low or RouletteBetType.High => betAmount * 2m, // 1:1 + stake
            _ => 0m
        };
}