using System.Text.Json;
using FairBank.Payments.Application.Exchange.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FairBank.Payments.Infrastructure.Services;

public sealed class ExchangeRateService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<ExchangeRateService> logger) : IExchangeRateService
{
    private const string CacheKey = "exchange_rates_czk";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private const string PrimaryUrl = "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/czk.json";
    private const string FallbackUrl = "https://latest.currency-api.pages.dev/v1/currencies/czk.json";

    public async Task<ExchangeRateResult?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken ct = default)
    {
        var data = await GetCachedRatesAsync(ct);
        if (data is null) return null;

        var from = fromCurrency.ToLowerInvariant();
        var to = toCurrency.ToLowerInvariant();

        decimal rate;
        if (from == "czk" && data.Rates.TryGetValue(to, out var toRate))
            rate = toRate;
        else if (to == "czk" && data.Rates.TryGetValue(from, out var fromRate) && fromRate != 0)
            rate = 1m / fromRate;
        else if (data.Rates.TryGetValue(from, out var fRate) && fRate != 0 && data.Rates.TryGetValue(to, out var tRate))
            rate = tRate / fRate;
        else
            return null;

        return new ExchangeRateResult(rate, fromCurrency.ToUpperInvariant(), toCurrency.ToUpperInvariant(), data.Date);
    }

    private async Task<CachedRateData?> GetCachedRatesAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out CachedRateData? cached))
            return cached;

        var json = await FetchRatesJsonAsync(ct);
        if (json is null) return null;

        var parsed = ParseRatesJson(json);
        if (parsed is null) return null;

        cache.Set(CacheKey, parsed, CacheTtl);
        return parsed;
    }

    private async Task<string?> FetchRatesJsonAsync(CancellationToken ct)
    {
        try { return await httpClient.GetStringAsync(PrimaryUrl, ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Primary exchange rate URL failed, trying fallback");
        }
        try { return await httpClient.GetStringAsync(FallbackUrl, ct); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Both exchange rate URLs failed");
            return null;
        }
    }

    private static CachedRateData? ParseRatesJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var date = root.GetProperty("date").GetString() ?? "";
            var czkElement = root.GetProperty("czk");
            var rates = new Dictionary<string, decimal>();
            foreach (var prop in czkElement.EnumerateObject())
                if (prop.Value.TryGetDecimal(out var rate))
                    rates[prop.Name] = rate;
            return new CachedRateData(date, rates);
        }
        catch { return null; }
    }

    private sealed record CachedRateData(string Date, Dictionary<string, decimal> Rates);
}
