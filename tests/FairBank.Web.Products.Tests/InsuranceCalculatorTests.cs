using FairBank.Web.Products.Services;

namespace FairBank.Web.Products.Tests;

public class InsuranceCalculatorTests
{
    [Theory]
    [InlineData("europe", "standard", 1, 1, 35)]
    [InlineData("europe", "plus", 1, 1, 65)]
    [InlineData("world", "standard", 1, 1, 75)]
    [InlineData("world", "plus", 1, 1, 120)]
    [InlineData("europe", "standard", 7, 2, 490)]
    [InlineData("world", "plus", 14, 3, 5040)]
    public void CalculateTravel_ReturnsCorrectPrice(
        string destination, string variant, int days, int persons, decimal expected)
    {
        InsuranceCalculator.CalculateTravel(destination, variant, days, persons).Should().Be(expected);
    }

    [Theory]
    [InlineData("apartment", 2_000_000, false, 1600)]
    [InlineData("house", 5_000_000, false, 6000)]
    [InlineData("apartment", 2_000_000, true, 2240)]
    [InlineData("house", 5_000_000, true, 8400)]
    public void CalculateProperty_ReturnsCorrectAnnualPremium(
        string type, decimal value, bool includeContents, decimal expected)
    {
        InsuranceCalculator.CalculatePropertyAnnual(type, value, includeContents).Should().Be(expected);
    }

    [Fact]
    public void CalculateLife_Age30_1M_Risk_ReturnsReasonableAmount()
    {
        var monthly = InsuranceCalculator.CalculateLifeMonthly(30, 1_000_000m, "risk");
        monthly.Should().BeInRange(200m, 500m);
    }

    [Fact]
    public void CalculateLife_Age50_HigherThanAge30()
    {
        var young = InsuranceCalculator.CalculateLifeMonthly(30, 1_000_000m, "risk");
        var older = InsuranceCalculator.CalculateLifeMonthly(50, 1_000_000m, "risk");
        older.Should().BeGreaterThan(young);
    }

    [Fact]
    public void CalculateLife_Investment_HigherThanRisk()
    {
        var risk = InsuranceCalculator.CalculateLifeMonthly(30, 1_000_000m, "risk");
        var invest = InsuranceCalculator.CalculateLifeMonthly(30, 1_000_000m, "investment");
        invest.Should().BeGreaterThan(risk);
    }

    [Theory]
    [InlineData(10000, "standard", 550)]
    [InlineData(10000, "plus", 850)]
    [InlineData(5000, "standard", 275)]
    public void CalculatePaymentProtection_ReturnsCorrectAmount(
        decimal monthlyPayment, string variant, decimal expected)
    {
        InsuranceCalculator.CalculatePaymentProtection(monthlyPayment, variant).Should().Be(expected);
    }
}
