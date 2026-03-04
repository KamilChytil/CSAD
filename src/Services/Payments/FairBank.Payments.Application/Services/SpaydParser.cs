using System.Globalization;

namespace FairBank.Payments.Application.Services;

public sealed record SpaydData(
    string AccountNumber,
    decimal? Amount,
    string Currency,
    string? Message);

public static class SpaydParser
{
    /// <summary>
    /// Parses a SPAYD string into structured data.
    /// </summary>
    public static SpaydData? Parse(string spayd)
    {
        if (string.IsNullOrWhiteSpace(spayd) || !spayd.StartsWith("SPD*"))
            return null;

        var parts = spayd.Split('*');
        string? account = null;
        decimal? amount = null;
        string currency = "CZK";
        string? message = null;

        foreach (var part in parts)
        {
            if (part.StartsWith("ACC:"))
                account = part[4..];
            else if (part.StartsWith("AM:"))
                amount = decimal.TryParse(part[3..], NumberStyles.Any, CultureInfo.InvariantCulture, out var a) ? a : null;
            else if (part.StartsWith("CC:"))
                currency = part[3..];
            else if (part.StartsWith("MSG:"))
                message = part[4..];
        }

        if (account is null)
            return null;

        return new SpaydData(account, amount, currency, message);
    }
}
