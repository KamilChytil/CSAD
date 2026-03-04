using FairBank.Payments.Domain.Enums;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Entities;

public sealed class Payment : AggregateRoot<Guid>
{
    public Guid SenderAccountId { get; private set; }
    public Guid? RecipientAccountId { get; private set; }
    public string SenderAccountNumber { get; private set; } = null!;
    public string RecipientAccountNumber { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public Currency Currency { get; private set; }
    public string? Description { get; private set; }
    public PaymentType Type { get; private set; }
    public PaymentStatus Status { get; private set; }
    public PaymentCategory Category { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // Template reference
    public Guid? TemplateId { get; private set; }

    // Standing order reference
    public Guid? StandingOrderId { get; private set; }

    private Payment() { } // EF Core

    public static Payment Create(
        Guid senderAccountId,
        string senderAccountNumber,
        string recipientAccountNumber,
        decimal amount,
        Currency currency,
        PaymentType type,
        string? description = null,
        Guid? recipientAccountId = null,
        Guid? templateId = null,
        Guid? standingOrderId = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be positive.", nameof(amount));

        if (string.IsNullOrWhiteSpace(senderAccountNumber))
            throw new ArgumentException("Sender account number is required.", nameof(senderAccountNumber));

        if (string.IsNullOrWhiteSpace(recipientAccountNumber))
            throw new ArgumentException("Recipient account number is required.", nameof(recipientAccountNumber));

        return new Payment
        {
            Id = Guid.NewGuid(),
            SenderAccountId = senderAccountId,
            RecipientAccountId = recipientAccountId,
            SenderAccountNumber = senderAccountNumber.Trim(),
            RecipientAccountNumber = recipientAccountNumber.Trim(),
            Amount = Math.Round(amount, 2),
            Currency = currency,
            Type = type,
            Description = description?.Trim(),
            Status = PaymentStatus.Pending,
            Category = PaymentCategory.Other,
            CreatedAt = DateTime.UtcNow,
            TemplateId = templateId,
            StandingOrderId = standingOrderId
        };
    }

    public void MarkCompleted(Guid? recipientAccountId = null)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Cannot complete payment in status {Status}.");

        Status = PaymentStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        if (recipientAccountId.HasValue)
            RecipientAccountId = recipientAccountId.Value;
    }

    public void MarkFailed(string reason)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Cannot fail payment in status {Status}.");

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
    }

    public void SetCategory(PaymentCategory category)
    {
        Category = category;
    }

    public void Cancel()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel payment in status {Status}.");

        Status = PaymentStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkPendingApproval()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Cannot mark as pending approval from {Status}.");

        Status = PaymentStatus.PendingApproval;
    }
}
