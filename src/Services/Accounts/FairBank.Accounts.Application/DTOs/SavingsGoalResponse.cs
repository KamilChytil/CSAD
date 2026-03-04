namespace FairBank.Accounts.Application.DTOs;

public sealed record SavingsGoalResponse(
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
