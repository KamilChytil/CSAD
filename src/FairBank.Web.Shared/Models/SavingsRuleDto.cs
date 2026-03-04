namespace FairBank.Web.Shared.Models;

public sealed record SavingsRuleDto(
    Guid Id,
    Guid AccountId,
    string Name,
    string? Description,
    string Type,
    decimal Amount,
    bool IsEnabled,
    DateTime CreatedAt);
