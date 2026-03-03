# Currency Exchange Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Extract conversion logic from Exchange.razor into a testable `CurrencyConverter` class, expand the currency dropdown to ~50 fiat currencies, and add unit tests.

**Architecture:** Pure C# `CurrencyConverter` class handles JSON parsing and conversion math via CZK pivot. Razor page delegates to it. New xUnit test project covers conversion and parsing.

**Tech Stack:** .NET 10, Blazor WASM, xUnit, FluentAssertions, fawazahmed0/currency-api

---

### Task 1: Create CurrencyConverter service class

**Files:**
- Create: `src/FairBank.Web.Exchange/Services/CurrencyConverter.cs`

**Step 1: Create the CurrencyConverter class**

```csharp
// src/FairBank.Web.Exchange/Services/CurrencyConverter.cs
using System.Text.Json;

namespace FairBank.Web.Exchange.Services;

public class CurrencyConverter
{
    private readonly Dictionary<string, decimal> _rates = new();

    public string? RateDate { get; private set; }

    // ~50 fiat ISO 4217 codes
    public static readonly HashSet<string> FiatCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "czk", "eur", "usd", "gbp", "chf", "pln", "jpy", "sek", "nok", "dkk",
        "huf", "cad", "aud", "nzd", "try", "brl", "mxn", "ars", "zar", "inr",
        "cny", "krw", "thb", "php", "idr", "myr", "sgd", "hkd", "twd", "rub",
        "uah", "ron", "bgn", "hrk", "rsd", "isk", "ils", "aed", "sar", "qar",
        "kwd", "egp", "ngn", "kes", "clp", "cop", "pen", "vnd", "bdt", "pkr", "lkr"
    };

    // Currency display names (Czech)
    public static readonly Dictionary<string, string> CurrencyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["czk"] = "Česká koruna", ["eur"] = "Euro", ["usd"] = "Americký dolar",
        ["gbp"] = "Britská libra", ["chf"] = "Švýcarský frank", ["pln"] = "Polský zlotý",
        ["jpy"] = "Japonský jen", ["sek"] = "Švédská koruna", ["nok"] = "Norská koruna",
        ["dkk"] = "Dánská koruna", ["huf"] = "Maďarský forint", ["cad"] = "Kanadský dolar",
        ["aud"] = "Australský dolar", ["nzd"] = "Novozélandský dolar", ["try"] = "Turecká lira",
        ["brl"] = "Brazilský real", ["mxn"] = "Mexické peso", ["ars"] = "Argentinské peso",
        ["zar"] = "Jihoafrický rand", ["inr"] = "Indická rupie", ["cny"] = "Čínský jüan",
        ["krw"] = "Jihokorejský won", ["thb"] = "Thajský baht", ["php"] = "Filipínské peso",
        ["idr"] = "Indonéská rupie", ["myr"] = "Malajsijský ringgit", ["sgd"] = "Singapurský dolar",
        ["hkd"] = "Hongkongský dolar", ["twd"] = "Tchajwanský dolar", ["rub"] = "Ruský rubl",
        ["uah"] = "Ukrajinská hřivna", ["ron"] = "Rumunský leu", ["bgn"] = "Bulharský lev",
        ["hrk"] = "Chorvatská kuna", ["rsd"] = "Srbský dinár", ["isk"] = "Islandská koruna",
        ["ils"] = "Izraelský šekel", ["aed"] = "Dirham SAE", ["sar"] = "Saúdský rijál",
        ["qar"] = "Katarský rijál", ["kwd"] = "Kuvajtský dinár", ["egp"] = "Egyptská libra",
        ["ngn"] = "Nigerijská naira", ["kes"] = "Keňský šilink", ["clp"] = "Chilské peso",
        ["cop"] = "Kolumbijské peso", ["pen"] = "Peruánský sol", ["vnd"] = "Vietnamský dong",
        ["bdt"] = "Bangladéšská taka", ["pkr"] = "Pákistánská rupie", ["lkr"] = "Srílanská rupie"
    };

    // Currency flags
    public static readonly Dictionary<string, string> CurrencyFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["czk"] = "🇨🇿", ["eur"] = "🇪🇺", ["usd"] = "🇺🇸", ["gbp"] = "🇬🇧",
        ["chf"] = "🇨🇭", ["pln"] = "🇵🇱", ["jpy"] = "🇯🇵", ["sek"] = "🇸🇪",
        ["nok"] = "🇳🇴", ["dkk"] = "🇩🇰", ["huf"] = "🇭🇺", ["cad"] = "🇨🇦",
        ["aud"] = "🇦🇺", ["nzd"] = "🇳🇿", ["try"] = "🇹🇷", ["brl"] = "🇧🇷",
        ["mxn"] = "🇲🇽", ["ars"] = "🇦🇷", ["zar"] = "🇿🇦", ["inr"] = "🇮🇳",
        ["cny"] = "🇨🇳", ["krw"] = "🇰🇷", ["thb"] = "🇹🇭", ["php"] = "🇵🇭",
        ["idr"] = "🇮🇩", ["myr"] = "🇲🇾", ["sgd"] = "🇸🇬", ["hkd"] = "🇭🇰",
        ["twd"] = "🇹🇼", ["rub"] = "🇷🇺", ["uah"] = "🇺🇦", ["ron"] = "🇷🇴",
        ["bgn"] = "🇧🇬", ["hrk"] = "🇭🇷", ["rsd"] = "🇷🇸", ["isk"] = "🇮🇸",
        ["ils"] = "🇮🇱", ["aed"] = "🇦🇪", ["sar"] = "🇸🇦", ["qar"] = "🇶🇦",
        ["kwd"] = "🇰🇼", ["egp"] = "🇪🇬", ["ngn"] = "🇳🇬", ["kes"] = "🇰🇪",
        ["clp"] = "🇨🇱", ["cop"] = "🇨🇴", ["pen"] = "🇵🇪", ["vnd"] = "🇻🇳",
        ["bdt"] = "🇧🇩", ["pkr"] = "🇵🇰", ["lkr"] = "🇱🇰"
    };

    /// <summary>
    /// Parse JSON from the currency API. Expected format:
    /// { "date": "2026-03-03", "czk": { "eur": 0.041, "usd": 0.048, ... } }
    /// </summary>
    public void LoadRates(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("date", out var dateProp))
            RateDate = dateProp.GetString();

        if (root.TryGetProperty("czk", out var rates))
        {
            _rates.Clear();
            foreach (var prop in rates.EnumerateObject())
            {
                if (prop.Value.TryGetDecimal(out var val) && val > 0)
                    _rates[prop.Name] = val;
            }
        }
    }

    /// <summary>
    /// Get all loaded rates (1 CZK = X of target currency).
    /// </summary>
    public IReadOnlyDictionary<string, decimal> GetAllRates() => _rates;

    /// <summary>
    /// Get only fiat currency rates.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> GetFiatRates()
        => _rates
            .Where(kv => FiatCurrencies.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>
    /// Convert amount between any two currencies via CZK pivot.
    /// rates[x] = how many x per 1 CZK.
    /// X→CZK: amount / rates[x]
    /// CZK→Y: amount * rates[y]
    /// X→Y:   (amount / rates[x]) * rates[y]
    /// </summary>
    public decimal? Convert(decimal amount, string from, string to)
    {
        from = from.ToLowerInvariant();
        to = to.ToLowerInvariant();

        if (from == to)
            return amount;

        if (amount == 0)
            return 0;

        // Step 1: convert to CZK
        decimal amountInCzk;
        if (from == "czk")
        {
            amountInCzk = amount;
        }
        else if (_rates.TryGetValue(from, out var fromRate) && fromRate > 0)
        {
            amountInCzk = amount / fromRate;
        }
        else
        {
            return null; // unknown source currency
        }

        // Step 2: convert from CZK to target
        if (to == "czk")
        {
            return amountInCzk;
        }
        else if (_rates.TryGetValue(to, out var toRate))
        {
            return amountInCzk * toRate;
        }
        else
        {
            return null; // unknown target currency
        }
    }

    /// <summary>
    /// Get display name for a currency code. Falls back to uppercase code.
    /// </summary>
    public static string GetName(string code)
        => CurrencyNames.TryGetValue(code, out var name) ? name : code.ToUpperInvariant();

    /// <summary>
    /// Get flag emoji for a currency code. Falls back to empty string.
    /// </summary>
    public static string GetFlag(string code)
        => CurrencyFlags.TryGetValue(code, out var flag) ? flag : "";
}
```

**Step 2: Verify build**

Run: `dotnet build src/FairBank.Web.Exchange/FairBank.Web.Exchange.csproj`
Expected: Build succeeded, 0 errors

**Step 3: Commit**

```bash
git add src/FairBank.Web.Exchange/Services/CurrencyConverter.cs
git commit -m "feat: add CurrencyConverter service class with fiat filtering"
```

---

### Task 2: Create test project and write unit tests

**Files:**
- Create: `tests/FairBank.Web.Exchange.Tests/FairBank.Web.Exchange.Tests.csproj`
- Create: `tests/FairBank.Web.Exchange.Tests/CurrencyConverterTests.cs`
- Modify: `FairBank.slnx` — add test project

**Step 1: Create test project file**

```xml
<!-- tests/FairBank.Web.Exchange.Tests/FairBank.Web.Exchange.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/FairBank.Web.Exchange/FairBank.Web.Exchange.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentAssertions" />
  </ItemGroup>
</Project>
```

**Step 2: Add to solution file**

In `FairBank.slnx`, add inside `<Folder Name="/tests/">`:
```xml
<Project Path="tests/FairBank.Web.Exchange.Tests/FairBank.Web.Exchange.Tests.csproj" />
```

**Step 3: Write all unit tests**

```csharp
// tests/FairBank.Web.Exchange.Tests/CurrencyConverterTests.cs
using FairBank.Web.Exchange.Services;

namespace FairBank.Web.Exchange.Tests;

public class CurrencyConverterTests
{
    // Sample JSON matching real API format
    private const string SampleJson = """
    {
        "date": "2026-03-03",
        "czk": {
            "eur": 0.04,
            "usd": 0.05,
            "gbp": 0.035,
            "btc": 0.0000001,
            "eth": 0.000001
        }
    }
    """;

    private CurrencyConverter CreateLoadedConverter()
    {
        var converter = new CurrencyConverter();
        converter.LoadRates(SampleJson);
        return converter;
    }

    // ── Conversion tests ──

    [Fact]
    public void Convert_CzkToEur_UsesDirectRate()
    {
        var converter = CreateLoadedConverter();
        // 1 CZK = 0.04 EUR, so 1000 CZK = 40 EUR
        var result = converter.Convert(1000m, "czk", "eur");
        result.Should().Be(40m);
    }

    [Fact]
    public void Convert_EurToCzk_UsesInverseRate()
    {
        var converter = CreateLoadedConverter();
        // 1 CZK = 0.04 EUR → 1 EUR = 25 CZK, so 100 EUR = 2500 CZK
        var result = converter.Convert(100m, "eur", "czk");
        result.Should().Be(2500m);
    }

    [Fact]
    public void Convert_EurToUsd_CrossCurrencyViaCzkPivot()
    {
        var converter = CreateLoadedConverter();
        // 100 EUR → CZK: 100 / 0.04 = 2500 CZK → USD: 2500 * 0.05 = 125 USD
        var result = converter.Convert(100m, "eur", "usd");
        result.Should().Be(125m);
    }

    [Fact]
    public void Convert_SameCurrency_ReturnsSameAmount()
    {
        var converter = CreateLoadedConverter();
        var result = converter.Convert(42m, "eur", "eur");
        result.Should().Be(42m);
    }

    [Fact]
    public void Convert_UnknownCurrency_ReturnsNull()
    {
        var converter = CreateLoadedConverter();
        var result = converter.Convert(100m, "xyz", "eur");
        result.Should().BeNull();
    }

    [Fact]
    public void Convert_UnknownTargetCurrency_ReturnsNull()
    {
        var converter = CreateLoadedConverter();
        var result = converter.Convert(100m, "eur", "xyz");
        result.Should().BeNull();
    }

    [Fact]
    public void Convert_ZeroAmount_ReturnsZero()
    {
        var converter = CreateLoadedConverter();
        var result = converter.Convert(0m, "czk", "eur");
        result.Should().Be(0m);
    }

    [Fact]
    public void Convert_IsCaseInsensitive()
    {
        var converter = CreateLoadedConverter();
        var result = converter.Convert(1000m, "CZK", "EUR");
        result.Should().Be(40m);
    }

    // ── Parsing tests ──

    [Fact]
    public void LoadRates_ValidJson_ParsesRatesCorrectly()
    {
        var converter = CreateLoadedConverter();
        var rates = converter.GetAllRates();
        rates.Should().ContainKey("eur").WhoseValue.Should().Be(0.04m);
        rates.Should().ContainKey("usd").WhoseValue.Should().Be(0.05m);
        rates.Should().ContainKey("gbp").WhoseValue.Should().Be(0.035m);
    }

    [Fact]
    public void LoadRates_ExtractsDate()
    {
        var converter = CreateLoadedConverter();
        converter.RateDate.Should().Be("2026-03-03");
    }

    [Fact]
    public void GetFiatRates_FiltersOutCrypto()
    {
        var converter = CreateLoadedConverter();
        var fiatRates = converter.GetFiatRates();
        fiatRates.Should().ContainKey("eur");
        fiatRates.Should().ContainKey("usd");
        fiatRates.Should().NotContainKey("btc");
        fiatRates.Should().NotContainKey("eth");
    }

    [Fact]
    public void LoadRates_EmptyRatesObject_HandlesGracefully()
    {
        var json = """{"date": "2026-01-01", "czk": {}}""";
        var converter = new CurrencyConverter();
        converter.LoadRates(json);
        converter.GetAllRates().Should().BeEmpty();
        converter.RateDate.Should().Be("2026-01-01");
    }
}
```

**Step 4: Build and run tests**

Run: `dotnet test tests/FairBank.Web.Exchange.Tests/ -v minimal`
Expected: All 12 tests PASS

**Step 5: Commit**

```bash
git add tests/FairBank.Web.Exchange.Tests/ FairBank.slnx
git commit -m "test: add CurrencyConverter unit tests (12 tests)"
```

---

### Task 3: Refactor Exchange.razor to use CurrencyConverter

**Files:**
- Modify: `src/FairBank.Web.Exchange/Pages/Exchange.razor`

**Step 1: Rewrite Exchange.razor to delegate to CurrencyConverter**

The page should:
1. Create a `CurrencyConverter` instance
2. Fetch JSON and pass it to `converter.LoadRates(json)`
3. Use `converter.Convert()` for conversions
4. Use `converter.GetFiatRates()` for the dropdown (sorted by code, CZK first)
5. Keep using the 12 major currencies for the rate table
6. Display currency names from `CurrencyConverter.GetName()` and flags from `CurrencyConverter.GetFlag()`

Key changes:
- Replace inline `_czkRates` dictionary with `CurrencyConverter` instance
- Replace inline `_majorCurrencies` array with data from converter's static dictionaries
- Dropdown iterates `GetFiatRates().Keys` sorted alphabetically (CZK pinned first)
- Rate table still uses a hardcoded list of 12 major codes for readability
- Remove all inline parsing/conversion logic (now in `CurrencyConverter`)

The full `.razor` file is provided in the step below.

**Step 2: Build and verify**

Run: `dotnet build FairBank.slnx`
Expected: Build succeeded, 0 errors

**Step 3: Run all tests**

Run: `dotnet test FairBank.slnx --no-build -v minimal`
Expected: All tests pass (60 existing + 12 new = 72 total)

**Step 4: Commit**

```bash
git add src/FairBank.Web.Exchange/Pages/Exchange.razor
git commit -m "refactor: use CurrencyConverter service, expand to ~50 fiat currencies"
```

---

### Task 4: Final verification and push

**Step 1: Clean build from scratch**

Run: `dotnet clean FairBank.slnx && dotnet build FairBank.slnx`
Expected: Build succeeded

**Step 2: Run all tests**

Run: `dotnet test FairBank.slnx -v minimal`
Expected: 72 tests pass (34 Accounts + 26 Identity + 12 Exchange)

**Step 3: Commit any remaining changes and push**

```bash
git push -u origin feature/currency-exchange
```
