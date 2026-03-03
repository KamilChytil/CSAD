using System.Text.Json.Serialization;
using FairBank.Accounts.Domain.Enums;
using FairBank.SharedKernel.Domain;

namespace FairBank.Accounts.Domain.ValueObjects;

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    [JsonConstructor]
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Create(decimal amount, Currency currency)
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));

        return new Money(Math.Round(amount, 2), currency);
    }

    public static Money Zero(Currency currency) => new(0, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        if (Amount < other.Amount)
            throw new InvalidOperationException("Insufficient funds.");
        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot operate on different currencies: {Currency} vs {other.Currency}.");
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}
