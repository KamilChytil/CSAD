# Currency Exchange Feature Design

## Overview

Add a currency exchange rates page (`/kurzy`) to FairBank that displays live exchange rates and allows conversion between any fiat currencies. Data is fetched from a free API (no authentication required).

## API

**Provider:** `fawazahmed0/currency-api` via jsdelivr CDN

- Primary: `https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/czk.json`
- Fallback: `https://latest.currency-api.pages.dev/v1/currencies/czk.json`

**Response format:**
```json
{
  "date": "2026-03-03",
  "czk": {
    "eur": 0.041219,
    "usd": 0.048211,
    ...200+ entries...
  }
}
```

- `czk.eur = 0.041219` means 1 CZK = 0.041219 EUR
- Includes fiat, crypto, and commodities — we filter to fiat only

## Architecture

```
FairBank.Web.Exchange/
  Services/
    CurrencyConverter.cs       # parsing + conversion logic (pure C#)
  Pages/
    Exchange.razor              # UI, injects HttpClient, uses CurrencyConverter

tests/FairBank.Web.Exchange.Tests/
  CurrencyConverterTests.cs    # 8-10 unit tests
```

## CurrencyConverter Class

```csharp
public class CurrencyConverter
{
    // Known fiat ISO 4217 codes (~50)
    public static readonly HashSet<string> FiatCurrencies = [...];

    // Parse API JSON response, store rates
    void LoadRates(string json);

    // Convert amount between any two currencies via CZK pivot
    decimal? Convert(decimal amount, string from, string to);

    // Get rates filtered to fiat only
    IReadOnlyDictionary<string, decimal> GetFiatRates();

    // Date from API response
    string? RateDate { get; }
}
```

**Conversion logic (CZK pivot):**
- CZK→X: `amount * rates[x]`
- X→CZK: `amount / rates[x]`
- X→Y: `(amount / rates[x]) * rates[y]`
- Same currency: return amount unchanged

## UI Components

### Converter Card
- Amount input (decimal)
- From currency dropdown (~50 fiat currencies)
- Swap button
- To currency dropdown (~50 fiat currencies)
- Result display

### Rate Table Card
- Shows ~12 major currencies vs CZK
- Columns: flag, code, 1 CZK = X, 1 X = Y CZK
- Date from API in header

## Unit Tests

| Test | Description |
|------|-------------|
| Convert_CzkToEur | Direct rate lookup |
| Convert_EurToCzk | Inverse (divide by rate) |
| Convert_EurToUsd | Cross-currency via CZK pivot |
| Convert_SameCurrency | Returns same amount |
| Convert_UnknownCurrency | Returns null |
| Convert_ZeroAmount | Returns zero |
| LoadRates_ValidJson | Parses rates correctly |
| LoadRates_ExtractsDate | Gets date field |
| LoadRates_EmptyRates | Handles gracefully |
| GetFiatRates_FiltersOutCrypto | Only returns fiat codes |

## Navigation

- Route: `/kurzy`
- SideNav + BottomNav: icon 💱, label "Kurzy"
- Already wired up in current implementation
