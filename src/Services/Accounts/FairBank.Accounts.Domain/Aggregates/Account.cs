using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.Domain.Aggregates;

public sealed class Account
{
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public AccountNumber AccountNumber { get; private set; } = null!;
    public Money Balance { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Money? SpendingLimit { get; private set; }
    public bool RequiresApproval { get; private set; }
    public Money? ApprovalThreshold { get; private set; }

    private readonly List<object> _uncommittedEvents = [];

    private Account() { } // Marten rehydration

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
        IsActive = false;
    }

    public void SetSpendingLimit(Money limit, Money? approvalThreshold = null)
    {
        EnsureActive();
        SpendingLimit = limit;
        RequiresApproval = true;
        ApprovalThreshold = approvalThreshold ?? limit;

        RaiseEvent(new SpendingLimitSet(Id, limit.Amount, limit.Currency, DateTime.UtcNow));
    }

    public bool NeedsApproval(Money amount)
    {
        if (!RequiresApproval || ApprovalThreshold is null) return false;
        return amount.Amount > ApprovalThreshold.Amount;
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

    public void Apply(SpendingLimitSet @event)
    {
        SpendingLimit = Money.Create(@event.Limit, @event.Currency);
        RequiresApproval = true;
        ApprovalThreshold = SpendingLimit;
    }
}
