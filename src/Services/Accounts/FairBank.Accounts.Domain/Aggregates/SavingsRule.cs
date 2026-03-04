using System.Text.Json.Serialization;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;

namespace FairBank.Accounts.Domain.Aggregates;

public sealed class SavingsRule
{
    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public Guid AccountId { get; private set; }
    [JsonInclude] public string Name { get; private set; } = null!;
    [JsonInclude] public string? Description { get; private set; }
    [JsonInclude] public SavingsRuleType Type { get; private set; }
    [JsonInclude] public decimal Amount { get; private set; }
    [JsonInclude] public bool IsEnabled { get; private set; }
    [JsonInclude] public DateTime CreatedAt { get; private set; }

    private readonly List<object> _uncommittedEvents = [];

    [JsonConstructor]
    private SavingsRule() { } // Marten rehydration

    public static SavingsRule Create(Guid accountId, string name, string? description, SavingsRuleType type, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Savings rule name is required.", nameof(name));

        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        var rule = new SavingsRule
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Name = name,
            Description = description,
            Type = type,
            Amount = amount,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        rule.RaiseEvent(new SavingsRuleCreated(
            rule.Id,
            accountId,
            name,
            description,
            type,
            amount,
            DateTime.UtcNow));

        return rule;
    }

    public void Toggle()
    {
        IsEnabled = !IsEnabled;
        RaiseEvent(new SavingsRuleToggled(Id, IsEnabled, DateTime.UtcNow));
    }

    // --- Event Sourcing support ---

    public IReadOnlyList<object> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    private void RaiseEvent(object @event) => _uncommittedEvents.Add(@event);

    // Marten event sourcing: Apply methods for rehydration from events
    public void Apply(SavingsRuleCreated @event)
    {
        Id = @event.RuleId;
        AccountId = @event.AccountId;
        Name = @event.Name;
        Description = @event.Description;
        Type = @event.Type;
        Amount = @event.Amount;
        IsEnabled = true;
        CreatedAt = @event.OccurredAt;
    }

    public void Apply(SavingsRuleToggled @event)
    {
        IsEnabled = @event.IsEnabled;
    }
}
