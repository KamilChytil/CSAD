using System.Text.Json.Serialization;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.Domain.Aggregates;

public sealed class SavingsGoal
{
    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public Guid AccountId { get; private set; }
    [JsonInclude] public string Name { get; private set; } = null!;
    [JsonInclude] public string? Description { get; private set; }
    [JsonInclude] public Money TargetAmount { get; private set; } = null!;
    [JsonInclude] public Money CurrentAmount { get; private set; } = null!;
    [JsonInclude] public bool IsCompleted { get; private set; }
    [JsonInclude] public DateTime CreatedAt { get; private set; }
    [JsonInclude] public DateTime? CompletedAt { get; private set; }

    private readonly List<object> _uncommittedEvents = [];

    [JsonConstructor]
    private SavingsGoal() { } // Marten rehydration

    public static SavingsGoal Create(Guid accountId, string name, string? description, decimal targetAmount, Currency currency)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Savings goal name is required.", nameof(name));

        if (targetAmount <= 0)
            throw new ArgumentException("Target amount must be positive.", nameof(targetAmount));

        var goal = new SavingsGoal
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Name = name,
            Description = description,
            TargetAmount = Money.Create(targetAmount, currency),
            CurrentAmount = Money.Zero(currency),
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        goal.RaiseEvent(new SavingsGoalCreated(
            goal.Id,
            accountId,
            name,
            description,
            targetAmount,
            currency,
            DateTime.UtcNow));

        return goal;
    }

    public int ProgressPercent => TargetAmount.Amount == 0
        ? 0
        : Math.Clamp((int)(CurrentAmount.Amount / TargetAmount.Amount * 100), 0, 100);

    public void Deposit(Money amount)
    {
        if (IsCompleted)
            throw new InvalidOperationException("Savings goal is already completed.");

        CurrentAmount = CurrentAmount.Add(amount);

        RaiseEvent(new SavingsDeposited(Id, amount.Amount, amount.Currency, DateTime.UtcNow));

        if (CurrentAmount.Amount >= TargetAmount.Amount)
        {
            IsCompleted = true;
            CompletedAt = DateTime.UtcNow;
            RaiseEvent(new SavingsGoalCompleted(Id, CompletedAt.Value));
        }
    }

    public void Withdraw(Money amount)
    {
        CurrentAmount = CurrentAmount.Subtract(amount);

        RaiseEvent(new SavingsWithdrawn(Id, amount.Amount, amount.Currency, DateTime.UtcNow));
    }

    // --- Event Sourcing support ---

    public IReadOnlyList<object> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    private void RaiseEvent(object @event) => _uncommittedEvents.Add(@event);

    // Marten event sourcing: Apply methods for rehydration from events
    public void Apply(SavingsGoalCreated @event)
    {
        Id = @event.GoalId;
        AccountId = @event.AccountId;
        Name = @event.Name;
        Description = @event.Description;
        TargetAmount = Money.Create(@event.TargetAmount, @event.Currency);
        CurrentAmount = Money.Zero(@event.Currency);
        IsCompleted = false;
        CreatedAt = @event.OccurredAt;
    }

    public void Apply(SavingsDeposited @event)
    {
        CurrentAmount = CurrentAmount.Add(Money.Create(@event.Amount, @event.Currency));
    }

    public void Apply(SavingsWithdrawn @event)
    {
        CurrentAmount = CurrentAmount.Subtract(Money.Create(@event.Amount, @event.Currency));
    }

    public void Apply(SavingsGoalCompleted @event)
    {
        IsCompleted = true;
        CompletedAt = @event.OccurredAt;
    }
}
