using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.Domain.Aggregates;

public sealed class PendingTransaction
{
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public Guid RequestedBy { get; private set; }
    public Guid? ApproverId { get; private set; }
    public PendingTransactionStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    private readonly List<object> _uncommittedEvents = [];

    private PendingTransaction() { }

    public static PendingTransaction Create(
        Guid accountId,
        Money amount,
        string description,
        Guid requestedBy)
    {
        var tx = new PendingTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = amount,
            Description = description,
            RequestedBy = requestedBy,
            Status = PendingTransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        tx.RaiseEvent(new TransactionRequested(
            tx.Id, accountId, amount.Amount, amount.Currency, description, requestedBy, DateTime.UtcNow));

        return tx;
    }

    public void Approve(Guid approverId)
    {
        if (Status != PendingTransactionStatus.Pending)
            throw new InvalidOperationException("Transaction is not pending.");

        Status = PendingTransactionStatus.Approved;
        ApproverId = approverId;
        ResolvedAt = DateTime.UtcNow;

        RaiseEvent(new TransactionApproved(Id, approverId, DateTime.UtcNow));
    }

    public void Reject(Guid approverId, string reason)
    {
        if (Status != PendingTransactionStatus.Pending)
            throw new InvalidOperationException("Transaction is not pending.");

        Status = PendingTransactionStatus.Rejected;
        ApproverId = approverId;
        RejectionReason = reason;
        ResolvedAt = DateTime.UtcNow;

        RaiseEvent(new TransactionRejected(Id, approverId, reason, DateTime.UtcNow));
    }

    public IReadOnlyList<object> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();
    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();
    private void RaiseEvent(object @event) => _uncommittedEvents.Add(@event);

    // Marten Apply methods
    public void Apply(TransactionRequested @event)
    {
        Id = @event.TransactionId;
        AccountId = @event.AccountId;
        Amount = Money.Create(@event.Amount, @event.Currency);
        Description = @event.Description;
        RequestedBy = @event.RequestedBy;
        Status = PendingTransactionStatus.Pending;
        CreatedAt = @event.OccurredAt;
    }

    public void Apply(TransactionApproved @event)
    {
        Status = PendingTransactionStatus.Approved;
        ApproverId = @event.ApproverId;
        ResolvedAt = @event.OccurredAt;
    }

    public void Apply(TransactionRejected @event)
    {
        Status = PendingTransactionStatus.Rejected;
        ApproverId = @event.ApproverId;
        RejectionReason = @event.Reason;
        ResolvedAt = @event.OccurredAt;
    }
}
