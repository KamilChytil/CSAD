namespace FairBank.Web.Shared.Models;

public sealed record SavingsRuleDto(
    Guid Id,
    string Name,
    string Description,
    bool IsEnabled);
