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
    [JsonInclude] public Money? SpendingLimit { get; private set; }
    [JsonInclude] public string? Alias { get; private set; }
    [JsonInclude] public bool RequiresApproval { get; private set; }
    [JsonInclude] public Money? ApprovalThreshold { get; private set; }
    [JsonInclude] public AccountLimits? Limits { get; private set; }

    private readonly List<object> _uncommittedEvents = [];

    [JsonConstructor]
    private Account() { } // Marten rehydration

    public static Account Create(Guid ownerId, Currency currency, string? accountNumber = null)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            AccountNumber = AccountNumber.Create(accountNumber),
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

    public void Close()
    {
        EnsureActive();
        if (Balance.Amount != 0)
            throw new InvalidOperationException("Cannot close account with non-zero balance.");
        IsActive = false;
        RaiseEvent(new AccountClosed(Id, DateTime.UtcNow));
    }

    public void Rename(string? alias)
    {
        EnsureActive();
        Alias = alias?.Trim();
        RaiseEvent(new AccountRenamed(Id, Alias, DateTime.UtcNow));
    }

    public void SetAccountLimits(AccountLimits limits)
    {
        EnsureActive();
        Limits = limits;
        RaiseEvent(new AccountLimitsSet(
            Id,
            limits.DailyTransactionLimit,
            limits.MonthlyTransactionLimit,
            limits.SingleTransactionLimit,
            limits.DailyTransactionCount,
            limits.OnlinePaymentLimit,
            DateTime.UtcNow));
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

    public void Apply(AccountClosed @event)
    {
        IsActive = false;
    }

    public void Apply(AccountRenamed @event)
    {
        Alias = @event.Alias;
    }

    public void Apply(AccountLimitsSet @event)
    {
        Limits = AccountLimits.Create(
            @event.DailyTransactionLimit,
            @event.MonthlyTransactionLimit,
            @event.SingleTransactionLimit,
            @event.DailyTransactionCount,
            @event.OnlinePaymentLimit);
    }
}
