// tests/FairBank.Web.Exchange.Tests/CurrencyConverterTests.cs
using FairBank.Web.Exchange.Services;

namespace FairBank.Web.Exchange.Tests;

public class CurrencyConverterTests
{
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

    [Fact]
    public void Convert_CzkToEur_UsesDirectRate()
    {
        var converter = CreateLoadedConverter();
        var result = converter.Convert(1000m, "czk", "eur");
        result.Should().Be(40m);
    }

    [Fact]
    public void Convert_EurToCzk_UsesInverseRate()
    {
        var converter = CreateLoadedConverter();
        var result = converter.Convert(100m, "eur", "czk");
        result.Should().Be(2500m);
    }

    [Fact]
    public void Convert_EurToUsd_CrossCurrencyViaCzkPivot()
    {
        var converter = CreateLoadedConverter();
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
