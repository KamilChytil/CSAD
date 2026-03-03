using FairBank.Web.Products.Services;

namespace FairBank.Web.Products.Tests;

public class MortgageCalculatorTests
{
    [Fact]
    public void GetLtv_3200kLoan_4000kProperty_Returns80()
    {
        MortgageCalculator.GetLtv(3_200_000m, 4_000_000m).Should().Be(80m);
    }

    [Fact]
    public void GetLtv_ZeroProperty_ReturnsZero()
    {
        MortgageCalculator.GetLtv(1_000_000m, 0m).Should().Be(0m);
    }

    [Theory]
    [InlineData(1, 50, 5.29)]
    [InlineData(1, 75, 5.59)]
    [InlineData(3, 50, 4.89)]
    [InlineData(3, 75, 5.19)]
    [InlineData(5, 50, 4.49)]
    [InlineData(5, 75, 4.79)]
    [InlineData(10, 50, 4.99)]
    [InlineData(10, 75, 5.29)]
    public void GetInterestRate_ReturnsCorrectRate(int fixationYears, decimal ltv, decimal expectedRate)
    {
        MortgageCalculator.GetInterestRate(fixationYears, ltv).Should().Be(expectedRate);
    }

    [Fact]
    public void GetInterestRate_UnknownFixation_Returns5point29()
    {
        MortgageCalculator.GetInterestRate(7, 50m).Should().Be(5.29m);
    }

    [Fact]
    public void Calculate_StandardMortgage()
    {
        var result = MortgageCalculator.Calculate(4_000_000m, 3_200_000m, 25, 5);
        result.InterestRate.Should().Be(4.79m);
        result.Ltv.Should().Be(80m);
        result.OwnResources.Should().Be(800_000m);
        result.MonthlyPayment.Should().BeGreaterThan(0);
        result.TotalCost.Should().BeGreaterThan(3_200_000m);
    }
}
