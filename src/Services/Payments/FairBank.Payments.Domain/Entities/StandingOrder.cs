using FairBank.Payments.Domain.Enums;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Entities;

public sealed class StandingOrder : AggregateRoot<Guid>
{
    public Guid SenderAccountId { get; private set; }
    public string SenderAccountNumber { get; private set; } = null!;
    public string RecipientAccountNumber { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public Currency Currency { get; private set; }
    public string? Description { get; private set; }
    public RecurrenceInterval Interval { get; private set; }
    public DateTime NextExecutionDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastExecutedAt { get; private set; }
    public int ExecutionCount { get; private set; }

    private StandingOrder() { } // EF Core

    public static StandingOrder Create(
        Guid senderAccountId,
        string senderAccountNumber,
        string recipientAccountNumber,
        decimal amount,
        Currency currency,
        RecurrenceInterval interval,
        DateTime firstExecutionDate,
        string? description = null,
        DateTime? endDate = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Standing order amount must be positive.", nameof(amount));

        if (string.IsNullOrWhiteSpace(recipientAccountNumber))
            throw new ArgumentException("Recipient account number is required.", nameof(recipientAccountNumber));

        if (endDate.HasValue && endDate.Value <= firstExecutionDate)
            throw new ArgumentException("End date must be after first execution date.", nameof(endDate));

        return new StandingOrder
        {
            Id = Guid.NewGuid(),
            SenderAccountId = senderAccountId,
            SenderAccountNumber = senderAccountNumber.Trim(),
            RecipientAccountNumber = recipientAccountNumber.Trim(),
            Amount = Math.Round(amount, 2),
            Currency = currency,
            Description = description?.Trim(),
            Interval = interval,
            NextExecutionDate = firstExecutionDate.Date,
            EndDate = endDate?.Date,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ExecutionCount = 0
        };
    }

    public void RecordExecution()
    {
        if (!IsActive)
            throw new InvalidOperationException("Standing order is not active.");

        LastExecutedAt = DateTime.UtcNow;
        ExecutionCount++;
        NextExecutionDate = CalculateNextDate(NextExecutionDate, Interval);

        if (EndDate.HasValue && NextExecutionDate > EndDate.Value)
            IsActive = false;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        if (EndDate.HasValue && NextExecutionDate > EndDate.Value)
            throw new InvalidOperationException("Cannot activate — end date has passed.");

        IsActive = true;
    }

    public void Update(decimal? amount = null, string? description = null,
        RecurrenceInterval? interval = null, DateTime? endDate = null)
    {
        if (amount.HasValue)
        {
            if (amount.Value <= 0)
                throw new ArgumentException("Amount must be positive.", nameof(amount));
            Amount = Math.Round(amount.Value, 2);
        }

        if (description is not null)
            Description = description.Trim();

        if (interval.HasValue)
            Interval = interval.Value;

        if (endDate.HasValue)
            EndDate = endDate.Value.Date;
    }

    public bool IsDueForExecution(DateTime currentDate)
    {
        return IsActive && NextExecutionDate.Date <= currentDate.Date;
    }

    private static DateTime CalculateNextDate(DateTime current, RecurrenceInterval interval) => interval switch
    {
        RecurrenceInterval.Daily => current.AddDays(1),
        RecurrenceInterval.Weekly => current.AddDays(7),
        RecurrenceInterval.Monthly => current.AddMonths(1),
        RecurrenceInterval.Quarterly => current.AddMonths(3),
        RecurrenceInterval.Yearly => current.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(interval))
    };
}
