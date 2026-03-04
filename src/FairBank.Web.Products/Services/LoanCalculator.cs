namespace FairBank.Web.Products.Services;

public static class LoanCalculator
{
    public record LoanResult(
        decimal MonthlyPayment,
        decimal InterestRate,
        decimal Rpsn,
        decimal TotalCost);

    public static decimal GetInterestRate(decimal amount) => amount switch
    {
        <= 100_000m => 8.9m,
        <= 500_000m => 5.9m,
        <= 1_000_000m => 5.4m,
        _ => 4.9m
    };

    public static decimal GetRpsn(decimal interestRate) => interestRate + 0.2m;

    public static decimal CalculateMonthlyPayment(decimal principal, int months, decimal annualRate)
    {
        if (principal <= 0 || months <= 0) return 0m;
        var monthlyRate = annualRate / 100m / 12m;
        if (monthlyRate == 0) return principal / months;
        var factor = (decimal)Math.Pow((double)(1 + monthlyRate), months);
        return Math.Round(principal * monthlyRate * factor / (factor - 1), 0);
    }

    public static decimal GetTotalCost(decimal monthlyPayment, int months) => monthlyPayment * months;

    public static LoanResult Calculate(decimal amount, int months)
    {
        var rate = GetInterestRate(amount);
        var payment = CalculateMonthlyPayment(amount, months, rate);
        return new LoanResult(payment, rate, GetRpsn(rate), GetTotalCost(payment, months));
    }
}
