using FairBank.Payments.Application.Services;
using FluentAssertions;

namespace FairBank.Payments.UnitTests.Services;

public class SpaydGeneratorTests
{
    [Fact]
    public void Generate_WithAllFields_ShouldReturnValidSpayd()
    {
        var result = SpaydGenerator.Generate("000000-1234567890/8888", 1500.50m, "CZK", "Test payment");

        result.Should().StartWith("SPD*1.0");
        result.Should().Contain("ACC:000000-1234567890/8888");
        result.Should().Contain("AM:1500.50");
        result.Should().Contain("CC:CZK");
        result.Should().Contain("MSG:Test payment");
    }

    [Fact]
    public void Generate_WithoutOptionalFields_ShouldReturnMinimalSpayd()
    {
        var result = SpaydGenerator.Generate("000000-1234567890/8888");

        result.Should().Be("SPD*1.0*ACC:000000-1234567890/8888*CC:CZK");
    }

    [Fact]
    public void Generate_SanitizesMessage()
    {
        var result = SpaydGenerator.Generate("000000-1234567890/8888", message: "Test*with*stars");

        result.Should().Contain("MSG:Testwithstars");
    }
}
