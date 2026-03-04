using FairBank.SharedKernel.Domain;
using System.Text.RegularExpressions;

namespace FairBank.Identity.Domain.ValueObjects;

public sealed partial class PhoneNumber : ValueObject
{
    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber Create(string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phone, nameof(phone));
        var normalized = NormalizeRegex().Replace(phone.Trim(), "");

        if (normalized.Length < 9 || normalized.Length > 15)
            throw new ArgumentException($"Invalid phone number: {phone}", nameof(phone));

        return new PhoneNumber(normalized);
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    [GeneratedRegex(@"[\s\-\(\)]")]
    private static partial Regex NormalizeRegex();
}
