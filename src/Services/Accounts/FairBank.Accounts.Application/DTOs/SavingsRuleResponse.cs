using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Application.DTOs;

public sealed record SavingsRuleResponse(
    Guid Id,
    Guid AccountId,
    string Name,
    string? Description,
    SavingsRuleType Type,
    decimal Amount,
    bool IsEnabled,
    DateTime CreatedAt);
