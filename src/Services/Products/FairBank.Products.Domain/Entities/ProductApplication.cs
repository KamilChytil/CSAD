using FairBank.Products.Domain.Enums;
using FairBank.SharedKernel.Domain;

namespace FairBank.Products.Domain.Entities;

public sealed class ProductApplication : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public ProductType ProductType { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public string Parameters { get; private set; } = string.Empty;
    public decimal MonthlyPayment { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public string? Note { get; private set; }

    private ProductApplication() { } // EF Core

    public static ProductApplication Create(
        Guid userId,
        ProductType productType,
        string parameters,
        decimal monthlyPayment)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.");
        if (string.IsNullOrWhiteSpace(parameters)) throw new ArgumentException("Parameters are required.");
        if (monthlyPayment < 0) throw new ArgumentException("MonthlyPayment cannot be negative.");

        return new ProductApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductType = productType,
            Status = ApplicationStatus.Pending,
            Parameters = parameters,
            MonthlyPayment = monthlyPayment,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve(Guid reviewerId, string? note = null)
    {
        if (Status != ApplicationStatus.Pending)
            throw new InvalidOperationException($"Cannot approve application in status {Status}.");

        Status = ApplicationStatus.Active;
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = reviewerId;
        Note = note;
    }

    public void Reject(Guid reviewerId, string? note = null)
    {
        if (Status != ApplicationStatus.Pending)
            throw new InvalidOperationException($"Cannot reject application in status {Status}.");

        Status = ApplicationStatus.Rejected;
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = reviewerId;
        Note = note;
    }

    public void Cancel()
    {
        if (Status != ApplicationStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel application in status {Status}.");

        Status = ApplicationStatus.Cancelled;
    }
}
