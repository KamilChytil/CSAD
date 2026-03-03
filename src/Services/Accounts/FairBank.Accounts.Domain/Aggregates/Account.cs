using System.Text.Json.Serialization;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.Domain.Aggregates;

public sealed class Account
{
    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public Guid OwnerId { get; private set; }
    [JsonInclude] public AccountNumber AccountNumber { get; private set; } = null!;
    [JsonInclude] public Money Balance { get; private set; } = null!;
    [JsonInclude] public bool IsActive { get; private set; }
    [JsonInclude] public DateTime CreatedAt { get; private set; }

    [JsonIgnore]
    private readonly List<object> _uncommittedEvents = [];

    // For Marten/System.Text.Json deserialization
    [JsonConstructor]
    public Account() { }

    public static Account Create(Guid ownerId, Currency currency)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            AccountNumber = AccountNumber.Create(),
            Balance = Money.Zero(currency),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        account.RaiseEvent(new AccountCreated(
            account.Id,
            ownerId,
            account.AccountNumber.Value,
            currency,
            DateTime.UtcNow));

        return account;
    }

    public void Deposit(Money amount, string description)
    {
        EnsureInitialized();
        EnsureActive();

        Balance = Balance.Add(amount);

        RaiseEvent(new MoneyDeposited(
            Id,
            amount.Amount,
            amount.Currency,
            description,
            DateTime.UtcNow));
    }

    public void Withdraw(Money amount, string description)
    {
        EnsureInitialized();
        EnsureActive();

        Balance = Balance.Subtract(amount); // Throws if insufficient

        RaiseEvent(new MoneyWithdrawn(
            Id,
            amount.Amount,
            amount.Currency,
            description,
            DateTime.UtcNow));
    }

    public void Deactivate()
    {
        EnsureInitialized();
        IsActive = false;
    }

    // --- Event Sourcing support ---

    public IReadOnlyList<object> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    private void RaiseEvent(object @event) => _uncommittedEvents.Add(@event);

    private void EnsureActive()
    {
        if (!IsActive)
            throw new InvalidOperationException("Account is not active.");
    }

    private void EnsureInitialized()
    {
        if (Id == Guid.Empty)
            throw new InvalidOperationException("Account is not initialized. Use Account.Create(...) or rehydrate from events.");
    }

    // Marten event sourcing: Apply methods for rehydration from events
    public void Apply(AccountCreated @event)
    {
        Id = @event.AccountId;
        OwnerId = @event.OwnerId;
        AccountNumber = AccountNumber.Create(@event.AccountNumber);
        Balance = Money.Zero(@event.Currency);
        IsActive = true;
        CreatedAt = @event.OccurredAt;
    }

    public void Apply(MoneyDeposited @event)
    {
        Balance = Balance.Add(Money.Create(@event.Amount, @event.Currency));
    }

    public void Apply(MoneyWithdrawn @event)
    {
        Balance = Balance.Subtract(Money.Create(@event.Amount, @event.Currency));
    }
}