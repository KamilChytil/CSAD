namespace FairBank.Payments.Application.Exchange.Services;

public interface IExchangeRateService
{
    Task<ExchangeRateResult?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken ct = default);
}

public sealed record ExchangeRateResult(decimal Rate, string FromCurrency, string ToCurrency, string RateDate);
