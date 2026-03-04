using FairBank.Web.Products.Services;

namespace FairBank.Web.Products.Tests;

public class LoanCalculatorTests
{
    [Theory]
    [InlineData(50_000, 8.9)]
    [InlineData(100_000, 8.9)]
    [InlineData(100_001, 5.9)]
    [InlineData(500_000, 5.9)]
    [InlineData(500_001, 5.4)]
    [InlineData(1_000_000, 5.4)]
    [InlineData(1_000_001, 4.9)]
    public void GetInterestRate_ReturnsCorrectTier(decimal amount, decimal expectedRate)
    {
        LoanCalculator.GetInterestRate(amount).Should().Be(expectedRate);
    }

    [Fact]
    public void CalculateMonthlyPayment_200k_60months_5point9percent()
    {
        var payment = LoanCalculator.CalculateMonthlyPayment(200_000m, 60, 5.9m);
        payment.Should().BeApproximately(3856m, 5m);
    }

    [Fact]
    public void CalculateMonthlyPayment_ZeroAmount_ReturnsZero()
    {
        LoanCalculator.CalculateMonthlyPayment(0m, 60, 5.9m).Should().Be(0m);
    }

    [Fact]
    public void GetRpsn_AddsPointTwo()
    {
        LoanCalculator.GetRpsn(5.9m).Should().Be(6.1m);
    }

    [Fact]
    public void GetTotalCost_MultipliesPaymentByMonths()
    {
        LoanCalculator.GetTotalCost(3856m, 60).Should().Be(231_360m);
    }

    [Fact]
    public void Calculate_ReturnsAllFields()
    {
        var result = LoanCalculator.Calculate(200_000m, 60);
        result.InterestRate.Should().Be(5.9m);
        result.Rpsn.Should().Be(6.1m);
        result.MonthlyPayment.Should().BeGreaterThan(0);
        result.TotalCost.Should().BeGreaterThan(200_000m);
    }
}
