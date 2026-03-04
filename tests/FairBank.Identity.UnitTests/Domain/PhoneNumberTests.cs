using FairBank.Identity.Domain.ValueObjects;
using FluentAssertions;

namespace FairBank.Identity.UnitTests.Domain;

public class PhoneNumberTests
{
    [Fact]
    public void Create_WithValidPhone_ShouldSucceed()
    {
        var phone = PhoneNumber.Create("+420 123 456 789");
        phone.Value.Should().Be("+420123456789");
    }

    [Fact]
    public void Create_WithTooShortPhone_ShouldThrow()
    {
        var act = () => PhoneNumber.Create("123");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNull_ShouldThrow()
    {
        var act = () => PhoneNumber.Create(null!);
        act.Should().Throw<ArgumentException>();
    }
}
