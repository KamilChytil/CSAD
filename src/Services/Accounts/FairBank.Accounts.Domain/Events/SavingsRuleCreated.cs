using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record SavingsRuleCreated(
    Guid RuleId, Guid AccountId, string Name, string? Description,
    SavingsRuleType Type, decimal Amount, DateTime OccurredAt);
