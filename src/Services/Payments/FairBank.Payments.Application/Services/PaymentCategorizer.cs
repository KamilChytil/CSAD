using FairBank.Payments.Domain.Enums;

namespace FairBank.Payments.Application.Services;

public static class PaymentCategorizer
{
    private static readonly Dictionary<PaymentCategory, string[]> Keywords = new()
    {
        [PaymentCategory.Housing] = ["nájem", "hypotéka", "bydlení", "rent", "mortgage", "nájemné"],
        [PaymentCategory.Food] = ["potraviny", "restaurace", "jídlo", "grocery", "food", "albert", "lidl", "tesco", "billa", "kaufland"],
        [PaymentCategory.Transport] = ["benzín", "nafta", "mhd", "jízdenka", "taxi", "uber", "bolt", "doprava", "transport", "parkování"],
        [PaymentCategory.Entertainment] = ["kino", "divadlo", "netflix", "spotify", "zábava", "entertainment", "hra", "game"],
        [PaymentCategory.Health] = ["lékárna", "doktor", "nemocnice", "zdraví", "pharmacy", "hospital", "lékař"],
        [PaymentCategory.Shopping] = ["oblečení", "elektronika", "amazon", "alza", "mall", "eshop", "nákup"],
        [PaymentCategory.Savings] = ["spoření", "vklad", "savings", "investice", "fond"],
        [PaymentCategory.Salary] = ["mzda", "plat", "výplata", "salary", "wage", "odměna"],
        [PaymentCategory.Utilities] = ["elektřina", "plyn", "voda", "internet", "telefon", "pojištění", "energy", "utility"]
    };

    public static PaymentCategory Categorize(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return PaymentCategory.Other;

        var lower = description.ToLowerInvariant();

        foreach (var (category, keywords) in Keywords)
        {
            if (keywords.Any(k => lower.Contains(k)))
                return category;
        }

        return PaymentCategory.Other;
    }
}
