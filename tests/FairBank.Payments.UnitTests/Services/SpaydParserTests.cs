using FairBank.Payments.Application.Services;
using FluentAssertions;

namespace FairBank.Payments.UnitTests.Services;

public class SpaydParserTests
{
    [Fact]
    public void Parse_WithValidSpayd_ShouldReturnData()
    {
        var result = SpaydParser.Parse("SPD*1.0*ACC:000000-1234567890/8888*AM:1500.50*CC:CZK*MSG:Test");

        result.Should().NotBeNull();
        result!.AccountNumber.Should().Be("000000-1234567890/8888");
        result.Amount.Should().Be(1500.50m);
        result.Currency.Should().Be("CZK");
        result.Message.Should().Be("Test");
    }

    [Fact]
    public void Parse_WithInvalidString_ShouldReturnNull()
    {
        var result = SpaydParser.Parse("not a spayd");
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_WithMinimalSpayd_ShouldReturnData()
    {
        var result = SpaydParser.Parse("SPD*1.0*ACC:000000-1234567890/8888");

        result.Should().NotBeNull();
        result!.AccountNumber.Should().Be("000000-1234567890/8888");
        result.Amount.Should().BeNull();
    }
}
