using FluentAssertions;
using FairBank.Identity.Domain.ValueObjects;

namespace FairBank.Identity.UnitTests.Domain;

public class EmailTests
{
    [Fact]
    public void Create_WithValidEmail_ShouldSucceed()
    {
        var email = Email.Create("user@example.com");
        email.Value.Should().Be("user@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("@no-local-part.com")]
    [InlineData("no-at-sign")]
    public void Create_WithInvalidEmail_ShouldThrow(string invalidEmail)
    {
        var act = () => Email.Create(invalidEmail);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TwoEmails_WithSameValue_ShouldBeEqual()
    {
        var email1 = Email.Create("user@example.com");
        var email2 = Email.Create("user@example.com");
        email1.Should().Be(email2);
    }
}
