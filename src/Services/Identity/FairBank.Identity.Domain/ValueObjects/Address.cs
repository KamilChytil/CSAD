using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string ZipCode { get; }
    public string Country { get; }

    private Address(string street, string city, string zipCode, string country)
    {
        Street = street;
        City = city;
        ZipCode = zipCode;
        Country = country;
    }

    public static Address Create(string street, string city, string zipCode, string country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street, nameof(street));
        ArgumentException.ThrowIfNullOrWhiteSpace(city, nameof(city));
        ArgumentException.ThrowIfNullOrWhiteSpace(zipCode, nameof(zipCode));
        ArgumentException.ThrowIfNullOrWhiteSpace(country, nameof(country));

        var normalizedZip = zipCode.Trim().Replace(" ", "");
        if (normalizedZip.Length < 5)
            throw new ArgumentException("ZIP code must be at least 5 characters.", nameof(zipCode));

        return new Address(street.Trim(), city.Trim(), normalizedZip, country.Trim());
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Street;
        yield return City;
        yield return ZipCode;
        yield return Country;
    }
}
