using FairBank.Cards.Domain.Enums;
using FairBank.Cards.Domain.ValueObjects;
using FairBank.SharedKernel.Domain;

namespace FairBank.Cards.Domain.Aggregates;

public sealed class Card : AggregateRoot<Guid>
{
    public Guid AccountId { get; private set; }
    public Guid UserId { get; private set; }
    public CardNumber CardNumber { get; private set; } = null!;
    public string CardholderName { get; private set; } = null!;
    public DateOnly ExpirationDate { get; private set; }
    public CardType CardType { get; private set; }
    public CardBrand CardBrand { get; private set; }
    public CardStatus Status { get; private set; }
    public decimal DailyLimit { get; private set; }
    public decimal MonthlyLimit { get; private set; }
    public bool OnlinePaymentsEnabled { get; private set; }
    public bool ContactlessEnabled { get; private set; }
    public string? PinHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Card() { } // EF Core

    public static Card Issue(Guid accountId, Guid userId, string cardholderName,
        CardType cardType, CardBrand cardBrand)
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            CardNumber = CardNumber.Create(),
            CardholderName = cardholderName.Trim(),
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3)),
            CardType = cardType,
            CardBrand = cardBrand,
            Status = CardStatus.Active,
            DailyLimit = 50000,
            MonthlyLimit = 200000,
            OnlinePaymentsEnabled = true,
            ContactlessEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Block()
    {
        if (Status is CardStatus.Cancelled or CardStatus.Expired)
            throw new InvalidOperationException($"Cannot block card in status {Status}.");
        Status = CardStatus.Blocked;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unblock()
    {
        if (Status != CardStatus.Blocked)
            throw new InvalidOperationException("Card is not blocked.");
        Status = CardStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == CardStatus.Cancelled)
            throw new InvalidOperationException("Card is already cancelled.");
        Status = CardStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetLimits(decimal dailyLimit, decimal monthlyLimit)
    {
        if (dailyLimit < 0 || monthlyLimit < 0)
            throw new ArgumentException("Limits must be non-negative.");
        if (dailyLimit > monthlyLimit)
            throw new ArgumentException("Daily limit cannot exceed monthly limit.");
        DailyLimit = dailyLimit;
        MonthlyLimit = monthlyLimit;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSettings(bool onlinePaymentsEnabled, bool contactlessEnabled)
    {
        OnlinePaymentsEnabled = onlinePaymentsEnabled;
        ContactlessEnabled = contactlessEnabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public Card Renew()
    {
        if (Status == CardStatus.Cancelled)
            throw new InvalidOperationException("Cannot renew a cancelled card.");

        Status = CardStatus.Expired;
        UpdatedAt = DateTime.UtcNow;

        return new Card
        {
            Id = Guid.NewGuid(),
            AccountId = AccountId,
            UserId = UserId,
            CardNumber = CardNumber.Create(),
            CardholderName = CardholderName,
            ExpirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3)),
            CardType = CardType,
            CardBrand = CardBrand,
            Status = CardStatus.Active,
            DailyLimit = DailyLimit,
            MonthlyLimit = MonthlyLimit,
            OnlinePaymentsEnabled = OnlinePaymentsEnabled,
            ContactlessEnabled = ContactlessEnabled,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetPin(string pinHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pinHash);
        PinHash = pinHash;
        UpdatedAt = DateTime.UtcNow;
    }
}
