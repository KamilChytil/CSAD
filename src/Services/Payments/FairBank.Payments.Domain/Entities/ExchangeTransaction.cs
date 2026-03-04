using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Entities;

public sealed class ExchangeTransaction : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid SourceAccountId { get; private set; }
    public Guid TargetAccountId { get; private set; }
    public string FromCurrency { get; private set; } = string.Empty;
    public string ToCurrency { get; private set; } = string.Empty;
    public decimal SourceAmount { get; private set; }
    public decimal TargetAmount { get; private set; }
    public decimal ExchangeRate { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ExchangeTransaction() { }

    public static ExchangeTransaction Create(
        Guid userId, Guid sourceAccountId, Guid targetAccountId,
        string fromCurrency, string toCurrency,
        decimal sourceAmount, decimal targetAmount, decimal exchangeRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(toCurrency);
        if (sourceAmount <= 0) throw new ArgumentException("Source amount must be positive.", nameof(sourceAmount));
        if (targetAmount <= 0) throw new ArgumentException("Target amount must be positive.", nameof(targetAmount));
        if (exchangeRate <= 0) throw new ArgumentException("Exchange rate must be positive.", nameof(exchangeRate));
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Cannot exchange same currency.");

        return new ExchangeTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SourceAccountId = sourceAccountId,
            TargetAccountId = targetAccountId,
            FromCurrency = fromCurrency.ToUpperInvariant(),
            ToCurrency = toCurrency.ToUpperInvariant(),
            SourceAmount = sourceAmount,
            TargetAmount = targetAmount,
            ExchangeRate = exchangeRate,
            CreatedAt = DateTime.UtcNow
        };
    }
}
