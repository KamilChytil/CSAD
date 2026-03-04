namespace FairBank.Web.Shared.Models;

public sealed record SavingsGoalDto(
    Guid Id,
    Guid AccountId,
    string Name,
    string? Description,
    decimal TargetAmount,
    decimal CurrentAmount,
    int ProgressPercent,
    string Currency,
    bool IsCompleted,
    DateTime CreatedAt,
    DateTime? CompletedAt);
