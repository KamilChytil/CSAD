using FairBank.SharedKernel.Domain;

namespace FairBank.Accounts.Domain.ValueObjects;

public sealed class AccountNumber : ValueObject
{
    public string Value { get; }

    private AccountNumber(string value) => Value = value;

    public static AccountNumber Create(string? value = null)
    {
        var number = value ?? GenerateAccountNumber();
        return new AccountNumber(number);
    }

    private static string GenerateAccountNumber()
    {
        var random = Random.Shared;
        // Format: FAIR-XXXX-XXXX-XXXX (16 digits)
        return $"FAIR-{random.Next(1000, 9999)}-{random.Next(1000, 9999)}-{random.Next(1000, 9999)}";
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
