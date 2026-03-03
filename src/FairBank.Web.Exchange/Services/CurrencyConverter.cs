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
        "uah", "ron", "bgn", "rsd", "isk", "ils", "aed", "sar", "qar",
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
        ["rsd"] = "Srbský dinár", ["isk"] = "Islandská koruna",
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
        ["bgn"] = "🇧🇬", ["rsd"] = "🇷🇸", ["isk"] = "🇮🇸",
        ["ils"] = "🇮🇱", ["aed"] = "🇦🇪", ["sar"] = "🇸🇦", ["qar"] = "🇶🇦",
        ["kwd"] = "🇰🇼", ["egp"] = "🇪🇬", ["ngn"] = "🇳🇬", ["kes"] = "🇰🇪",
        ["clp"] = "🇨🇱", ["cop"] = "🇨🇴", ["pen"] = "🇵🇪", ["vnd"] = "🇻🇳",
        ["bdt"] = "🇧🇩", ["pkr"] = "🇵🇰", ["lkr"] = "🇱🇰"
    };

    public void LoadRates(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        _rates.Clear();
        RateDate = null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("date", out var dateProp))
            RateDate = dateProp.GetString();

        if (root.TryGetProperty("czk", out var rates))
        {
            foreach (var prop in rates.EnumerateObject())
            {
                if (prop.Value.TryGetDecimal(out var val) && val > 0)
                    _rates[prop.Name] = val;
            }
        }
    }

    public IReadOnlyDictionary<string, decimal> GetAllRates() => _rates;

    public IReadOnlyDictionary<string, decimal> GetFiatRates()
        => _rates
            .Where(kv => FiatCurrencies.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

    public decimal? Convert(decimal amount, string from, string to)
    {
        from = from.ToLowerInvariant();
        to = to.ToLowerInvariant();

        if (from == to)
            return amount;

        if (amount == 0)
            return 0;

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
            return null;
        }

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
            return null;
        }
    }

    public static string GetName(string code)
        => CurrencyNames.TryGetValue(code, out var name) ? name : code.ToUpperInvariant();

    public static string GetFlag(string code)
        => CurrencyFlags.TryGetValue(code, out var flag) ? flag : "";
}
