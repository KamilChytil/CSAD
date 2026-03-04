using System.Text.Json.Serialization;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.Domain.Aggregates;

public sealed class Investment
{
    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public Guid AccountId { get; private set; }
    [JsonInclude] public string Name { get; private set; } = null!;
    [JsonInclude] public InvestmentType Type { get; private set; }
    [JsonInclude] public Money InvestedAmount { get; private set; } = null!;
    [JsonInclude] public Money CurrentValue { get; private set; } = null!;
    [JsonInclude] public decimal Units { get; private set; }
    [JsonInclude] public decimal PricePerUnit { get; private set; }
    [JsonInclude] public bool IsActive { get; private set; }
    [JsonInclude] public DateTime CreatedAt { get; private set; }
    [JsonInclude] public DateTime? SoldAt { get; private set; }

    private readonly List<object> _uncommittedEvents = [];

    [JsonConstructor]
    private Investment() { } // Marten rehydration

    public static Investment Create(
        Guid accountId, string name, InvestmentType type,
        decimal investedAmount, decimal units, decimal pricePerUnit, Currency currency)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Investment name is required.", nameof(name));

        if (investedAmount <= 0)
            throw new ArgumentException("Invested amount must be positive.", nameof(investedAmount));

        if (units <= 0)
            throw new ArgumentException("Units must be positive.", nameof(units));

        if (pricePerUnit <= 0)
            throw new ArgumentException("Price per unit must be positive.", nameof(pricePerUnit));

        var investment = new Investment
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Name = name,
            Type = type,
            InvestedAmount = Money.Create(investedAmount, currency),
            CurrentValue = Money.Create(units * pricePerUnit, currency),
            Units = units,
            PricePerUnit = pricePerUnit,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        investment.RaiseEvent(new InvestmentCreated(
            investment.Id,
            accountId,
            name,
            type,
            investedAmount,
            units,
            pricePerUnit,
            currency,
            DateTime.UtcNow));

        return investment;
    }

    public decimal ChangePercent => InvestedAmount.Amount == 0
        ? 0
        : (CurrentValue.Amount - InvestedAmount.Amount) / InvestedAmount.Amount * 100;

    public void UpdateValue(decimal newPricePerUnit)
    {
        if (newPricePerUnit <= 0)
            throw new ArgumentException("Price per unit must be positive.", nameof(newPricePerUnit));

        PricePerUnit = newPricePerUnit;
        CurrentValue = Money.Create(Units * newPricePerUnit, CurrentValue.Currency);

        RaiseEvent(new InvestmentValueUpdated(Id, newPricePerUnit, DateTime.UtcNow));
    }

    public void Sell()
    {
        if (!IsActive)
            throw new InvalidOperationException("Investment is already sold.");

        IsActive = false;
        SoldAt = DateTime.UtcNow;

        RaiseEvent(new InvestmentSold(Id, CurrentValue.Amount, CurrentValue.Currency, SoldAt.Value));
    }

    // --- Event Sourcing support ---

    public IReadOnlyList<object> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    private void RaiseEvent(object @event) => _uncommittedEvents.Add(@event);

    // Marten event sourcing: Apply methods for rehydration from events
    public void Apply(InvestmentCreated @event)
    {
        Id = @event.InvestmentId;
        AccountId = @event.AccountId;
        Name = @event.Name;
        Type = @event.Type;
        InvestedAmount = Money.Create(@event.InvestedAmount, @event.Currency);
        CurrentValue = Money.Create(@event.Units * @event.PricePerUnit, @event.Currency);
        Units = @event.Units;
        PricePerUnit = @event.PricePerUnit;
        IsActive = true;
        CreatedAt = @event.OccurredAt;
    }

    public void Apply(InvestmentValueUpdated @event)
    {
        PricePerUnit = @event.NewPricePerUnit;
        CurrentValue = Money.Create(Units * @event.NewPricePerUnit, CurrentValue.Currency);
    }

    public void Apply(InvestmentSold @event)
    {
        IsActive = false;
        SoldAt = @event.OccurredAt;
    }
}
