using System.Text.Json.Serialization;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.Domain.Aggregates;

public sealed class Card
{
    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public Guid AccountId { get; private set; }
    [JsonInclude] public string CardNumber { get; private set; } = null!;
    [JsonInclude] public string HolderName { get; private set; } = null!;
    [JsonInclude] public DateTime ExpirationDate { get; private set; }
    [JsonInclude] public string CVV { get; private set; } = null!;
    [JsonInclude] public CardType Type { get; private set; }
    [JsonInclude] public bool IsActive { get; private set; }
    [JsonInclude] public bool IsFrozen { get; private set; }
    [JsonInclude] public Money? DailyLimit { get; private set; }
    [JsonInclude] public Money? MonthlyLimit { get; private set; }
    [JsonInclude] public bool OnlinePaymentsEnabled { get; private set; }
    [JsonInclude] public bool ContactlessEnabled { get; private set; }
    [JsonInclude] public DateTime CreatedAt { get; private set; }

    private readonly List<object> _uncommittedEvents = [];

    [JsonConstructor]
    private Card() { } // Marten rehydration

    public static Card Create(Guid accountId, string holderName, CardType type, Currency currency)
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            CardNumber = GenerateCardNumber(),
            HolderName = holderName,
            ExpirationDate = DateTime.UtcNow.AddYears(4),
            CVV = GenerateCVV(),
            Type = type,
            IsActive = true,
            IsFrozen = false,
            OnlinePaymentsEnabled = true,
            ContactlessEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        card.RaiseEvent(new CardIssued(
            card.Id,
            accountId,
            card.CardNumber,
            holderName,
            card.ExpirationDate,
            type,
            DateTime.UtcNow));

        return card;
    }

    public string MaskedNumber => $"**** **** **** {CardNumber[^4..]}";

    public void Freeze()
    {
        EnsureActive();

        if (IsFrozen)
            throw new InvalidOperationException("Card is already frozen.");

        IsFrozen = true;

        RaiseEvent(new CardFrozen(Id, DateTime.UtcNow));
    }

    public void Unfreeze()
    {
        EnsureActive();

        if (!IsFrozen)
            throw new InvalidOperationException("Card is not frozen.");

        IsFrozen = false;

        RaiseEvent(new CardUnfrozen(Id, DateTime.UtcNow));
    }

    public void SetLimits(decimal? dailyLimit, decimal? monthlyLimit, Currency currency)
    {
        EnsureActive();

        DailyLimit = dailyLimit.HasValue ? Money.Create(dailyLimit.Value, currency) : null;
        MonthlyLimit = monthlyLimit.HasValue ? Money.Create(monthlyLimit.Value, currency) : null;

        RaiseEvent(new CardLimitSet(Id, dailyLimit, monthlyLimit, currency, DateTime.UtcNow));
    }

    public void UpdateSettings(bool onlinePaymentsEnabled, bool contactlessEnabled)
    {
        EnsureActive();

        OnlinePaymentsEnabled = onlinePaymentsEnabled;
        ContactlessEnabled = contactlessEnabled;

        RaiseEvent(new CardSettingsChanged(Id, onlinePaymentsEnabled, contactlessEnabled, DateTime.UtcNow));
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Card is already deactivated.");

        IsActive = false;

        RaiseEvent(new CardDeactivated(Id, DateTime.UtcNow));
    }

    // --- Event Sourcing support ---

    public IReadOnlyList<object> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    private void RaiseEvent(object @event) => _uncommittedEvents.Add(@event);

    private void EnsureActive()
    {
        if (!IsActive)
            throw new InvalidOperationException("Card is not active.");
    }

    // Marten event sourcing: Apply methods for rehydration from events
    public void Apply(CardIssued @event)
    {
        Id = @event.CardId;
        AccountId = @event.AccountId;
        CardNumber = @event.CardNumber;
        HolderName = @event.HolderName;
        ExpirationDate = @event.ExpirationDate;
        Type = @event.Type;
        IsActive = true;
        IsFrozen = false;
        OnlinePaymentsEnabled = true;
        ContactlessEnabled = true;
        CreatedAt = @event.OccurredAt;
    }

    public void Apply(CardFrozen @event)
    {
        IsFrozen = true;
    }

    public void Apply(CardUnfrozen @event)
    {
        IsFrozen = false;
    }

    public void Apply(CardLimitSet @event)
    {
        DailyLimit = @event.DailyLimit.HasValue ? Money.Create(@event.DailyLimit.Value, @event.Currency) : null;
        MonthlyLimit = @event.MonthlyLimit.HasValue ? Money.Create(@event.MonthlyLimit.Value, @event.Currency) : null;
    }

    public void Apply(CardSettingsChanged @event)
    {
        OnlinePaymentsEnabled = @event.OnlinePaymentsEnabled;
        ContactlessEnabled = @event.ContactlessEnabled;
    }

    public void Apply(CardDeactivated @event)
    {
        IsActive = false;
    }

    /// <summary>
    /// Generates a Visa-format card number: 4xxx xxxx xxxx xxxx (16 digits starting with 4).
    /// </summary>
    private static string GenerateCardNumber()
    {
        var random = Random.Shared;
        var digits = new char[16];
        digits[0] = '4'; // Visa prefix
        for (var i = 1; i < 16; i++)
            digits[i] = (char)('0' + random.Next(0, 10));
        return new string(digits);
    }

    /// <summary>
    /// Generates a random 3-digit CVV.
    /// </summary>
    private static string GenerateCVV()
    {
        return Random.Shared.Next(100, 1000).ToString();
    }
}
