namespace FairBank.Accounts.Domain.Events;

public sealed record SavingsRuleToggled(
    Guid RuleId, bool IsEnabled, DateTime OccurredAt);
