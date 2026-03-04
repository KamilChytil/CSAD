using FairBank.Identity.Domain.ValueObjects;
using FluentAssertions;

namespace FairBank.Identity.UnitTests.Domain;

public class AddressTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var address = Address.Create("Hlavni 123", "Praha", "11000", "CZ");
        address.Street.Should().Be("Hlavni 123");
        address.City.Should().Be("Praha");
        address.ZipCode.Should().Be("11000");
        address.Country.Should().Be("CZ");
    }

    [Fact]
    public void Create_WithShortZip_ShouldThrow()
    {
        var act = () => Address.Create("Street", "City", "123", "CZ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NormalizesZipCode()
    {
        var address = Address.Create("Street", "City", "110 00", "CZ");
        address.ZipCode.Should().Be("11000");
    }
}
