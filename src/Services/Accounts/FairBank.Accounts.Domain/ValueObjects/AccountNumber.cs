using System.Text.Json.Serialization;
using FairBank.SharedKernel.Domain;

namespace FairBank.Accounts.Domain.ValueObjects;

public sealed class AccountNumber : ValueObject
{
    /// <summary>FairBank's bank code in the Czech clearing system.</summary>
    public const string FairBankCode = "8888";

    public string Value { get; }

    [JsonConstructor]
    private AccountNumber(string value) => Value = value;

    public static AccountNumber Create(string? value = null)
    {
        var number = value ?? GenerateAccountNumber();
        return new AccountNumber(number);
    }

    /// <summary>
    /// Generates a Czech-format account number: předčíslí-číslo/kód_banky.
    /// Example: 000000-1234567890/8888
    /// </summary>
    private static string GenerateAccountNumber()
    {
        var random = Random.Shared;
        var accountDigits = random.NextInt64(1_000_000_000L, 9_999_999_999L);
        return $"000000-{accountDigits:D10}/{FairBankCode}";
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
