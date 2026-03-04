using System.Text;

namespace FairBank.Payments.Application.Services;

public static class SpaydGenerator
{
    /// <summary>
    /// Generates a SPAYD (Short Payment Descriptor) string for Czech QR payments.
    /// Format: SPD*1.0*ACC:{accountNumber}*AM:{amount}*CC:{currency}*MSG:{message}
    /// </summary>
    public static string Generate(string accountNumber, decimal? amount = null, string currency = "CZK", string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber, nameof(accountNumber));

        var sb = new StringBuilder();
        sb.Append("SPD*1.0");
        sb.Append($"*ACC:{accountNumber.Replace(" ", "")}");

        if (amount.HasValue && amount.Value > 0)
            sb.Append($"*AM:{amount.Value:F2}");

        sb.Append($"*CC:{currency}");

        if (!string.IsNullOrWhiteSpace(message))
        {
            // SPAYD message max 60 chars, no * allowed
            var sanitized = message.Replace("*", "").Trim();
            if (sanitized.Length > 60)
                sanitized = sanitized[..60];
            sb.Append($"*MSG:{sanitized}");
        }

        return sb.ToString();
    }
}
