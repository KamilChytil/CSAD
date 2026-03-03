using FairBank.Payments.Domain.Enums;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Entities;

public sealed class PaymentTemplate : AggregateRoot<Guid>
{
    public Guid OwnerAccountId { get; private set; }
    public string Name { get; private set; } = null!;
    public string RecipientAccountNumber { get; private set; } = null!;
    public string? RecipientName { get; private set; }
    public decimal? DefaultAmount { get; private set; }
    public Currency Currency { get; private set; }
    public string? DefaultDescription { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    private PaymentTemplate() { } // EF Core

    public static PaymentTemplate Create(
        Guid ownerAccountId,
        string name,
        string recipientAccountNumber,
        Currency currency,
        string? recipientName = null,
        decimal? defaultAmount = null,
        string? defaultDescription = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(recipientAccountNumber))
            throw new ArgumentException("Recipient account number is required.", nameof(recipientAccountNumber));

        if (defaultAmount.HasValue && defaultAmount.Value <= 0)
            throw new ArgumentException("Default amount must be positive.", nameof(defaultAmount));

        return new PaymentTemplate
        {
            Id = Guid.NewGuid(),
            OwnerAccountId = ownerAccountId,
            Name = name.Trim(),
            RecipientAccountNumber = recipientAccountNumber.Trim(),
            RecipientName = recipientName?.Trim(),
            DefaultAmount = defaultAmount.HasValue ? Math.Round(defaultAmount.Value, 2) : null,
            Currency = currency,
            DefaultDescription = defaultDescription?.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    public void Update(string? name = null, string? recipientAccountNumber = null,
        string? recipientName = null, decimal? defaultAmount = null,
        string? defaultDescription = null)
    {
        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Template name cannot be empty.", nameof(name));
            Name = name.Trim();
        }

        if (recipientAccountNumber is not null)
        {
            if (string.IsNullOrWhiteSpace(recipientAccountNumber))
                throw new ArgumentException("Recipient account number cannot be empty.", nameof(recipientAccountNumber));
            RecipientAccountNumber = recipientAccountNumber.Trim();
        }

        if (recipientName is not null)
            RecipientName = recipientName.Trim();

        if (defaultAmount.HasValue)
        {
            if (defaultAmount.Value <= 0)
                throw new ArgumentException("Default amount must be positive.", nameof(defaultAmount));
            DefaultAmount = Math.Round(defaultAmount.Value, 2);
        }

        if (defaultDescription is not null)
            DefaultDescription = defaultDescription.Trim();

        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
