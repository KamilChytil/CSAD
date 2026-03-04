using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Entities;

public sealed class ExchangeFavorite : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string FromCurrency { get; private set; } = string.Empty;
    public string ToCurrency { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private ExchangeFavorite() { }

    public static ExchangeFavorite Create(Guid userId, string fromCurrency, string toCurrency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(toCurrency);
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Cannot favorite same currency pair.");

        return new ExchangeFavorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FromCurrency = fromCurrency.ToUpperInvariant(),
            ToCurrency = toCurrency.ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
