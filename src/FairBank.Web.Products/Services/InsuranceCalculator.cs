namespace FairBank.Web.Products.Services;

public static class InsuranceCalculator
{
    private static readonly Dictionary<(string dest, string variant), decimal> TravelRates = new()
    {
        [("europe", "standard")] = 35m,
        [("europe", "plus")] = 65m,
        [("world", "standard")] = 75m,
        [("world", "plus")] = 120m,
    };

    public static decimal CalculateTravel(string destination, string variant, int days, int persons)
    {
        var key = (destination.ToLowerInvariant(), variant.ToLowerInvariant());
        var rate = TravelRates.GetValueOrDefault(key, 35m);
        return rate * days * persons;
    }

    public static decimal CalculatePropertyAnnual(string propertyType, decimal propertyValue, bool includeContents)
    {
        var baseRate = propertyType.ToLowerInvariant() == "house" ? 0.0012m : 0.0008m;
        var annual = Math.Round(propertyValue * baseRate, 0);
        if (includeContents) annual = Math.Round(annual * 1.4m, 0);
        return annual;
    }

    public static decimal CalculatePropertyMonthly(string propertyType, decimal propertyValue, bool includeContents)
        => Math.Round(CalculatePropertyAnnual(propertyType, propertyValue, includeContents) / 12m, 0);

    public static decimal GetAgeCoefficient(int age) => age switch
    {
        <= 25 => 0.8m,
        <= 35 => 1.0m,
        <= 45 => 1.5m,
        <= 55 => 2.2m,
        _ => 3.0m
    };

    public static decimal CalculateLifeMonthly(int age, decimal coverageAmount, string variant)
    {
        var baseRate = variant.ToLowerInvariant() == "investment" ? 0.0048m : 0.0036m;
        var ageCoef = GetAgeCoefficient(age);
        return Math.Round(coverageAmount * baseRate * ageCoef / 12m, 0);
    }

    public static decimal CalculatePaymentProtection(decimal monthlyPayment, string variant)
    {
        var rate = variant.ToLowerInvariant() == "plus" ? 0.085m : 0.055m;
        return Math.Round(monthlyPayment * rate, 0);
    }
}
