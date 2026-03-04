namespace FairBank.Web.Products.Services;

public static class MortgageCalculator
{
    public record MortgageResult(
        decimal MonthlyPayment,
        decimal InterestRate,
        decimal Ltv,
        decimal OwnResources,
        decimal TotalCost);

    public static readonly int[] FixationOptions = [1, 3, 5, 10];

    public static decimal GetLtv(decimal loanAmount, decimal propertyPrice)
        => propertyPrice > 0 ? Math.Round(loanAmount / propertyPrice * 100, 1) : 0m;

    public static decimal GetInterestRate(int fixationYears, decimal ltv)
    {
        var isLowLtv = ltv <= 60m;
        return fixationYears switch
        {
            1  => isLowLtv ? 5.29m : 5.59m,
            3  => isLowLtv ? 4.89m : 5.19m,
            5  => isLowLtv ? 4.49m : 4.79m,
            10 => isLowLtv ? 4.99m : 5.29m,
            _  => isLowLtv ? 5.29m : 5.59m
        };
    }

    public static decimal CalculateMonthlyPayment(decimal principal, int years, decimal annualRate)
    {
        if (principal <= 0 || years <= 0) return 0m;
        var months = years * 12;
        var monthlyRate = annualRate / 100m / 12m;
        if (monthlyRate == 0) return principal / months;
        var factor = (decimal)Math.Pow((double)(1 + monthlyRate), months);
        return Math.Round(principal * monthlyRate * factor / (factor - 1), 0);
    }

    public static MortgageResult Calculate(decimal propertyPrice, decimal loanAmount, int years, int fixationYears)
    {
        var ltv = GetLtv(loanAmount, propertyPrice);
        var rate = GetInterestRate(fixationYears, ltv);
        var payment = CalculateMonthlyPayment(loanAmount, years, rate);
        var totalCost = payment * years * 12;
        return new MortgageResult(payment, rate, ltv, propertyPrice - loanAmount, totalCost);
    }
}
