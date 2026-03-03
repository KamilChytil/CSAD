using System.Text.RegularExpressions;
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.ValueObjects;

public sealed partial class Email : ValueObject
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));

        var trimmed = email.Trim().ToLowerInvariant();

        if (!EmailRegex().IsMatch(trimmed))
            throw new ArgumentException($"Invalid email format: {email}", nameof(email));

        return new Email(trimmed);
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
