using FairBank.SharedKernel.Domain;

namespace FairBank.Cards.Domain.ValueObjects;

public sealed class CardNumber : ValueObject
{
    public string FullNumber { get; }
    public string MaskedNumber { get; }
    public string LastFourDigits { get; }

    private CardNumber(string fullNumber)
    {
        FullNumber = fullNumber;
        LastFourDigits = fullNumber[^4..];
        MaskedNumber = $"**** **** **** {LastFourDigits}";
    }

    public static CardNumber Create(string? number = null)
    {
        var cardNumber = number ?? GenerateNumber();
        return new CardNumber(cardNumber);
    }

    private static string GenerateNumber()
    {
        var random = new Random();
        var digits = new char[16];
        digits[0] = '4'; // Visa prefix
        for (int i = 1; i < 16; i++)
            digits[i] = (char)('0' + random.Next(10));
        return new string(digits);
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return FullNumber;
    }
}
