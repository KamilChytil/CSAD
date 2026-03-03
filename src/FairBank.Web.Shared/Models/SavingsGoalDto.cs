namespace FairBank.Web.Shared.Models;

public sealed record SavingsGoalDto(
    Guid Id,
    string Name,
    string Description,
    decimal TargetAmount,
    decimal CurrentAmount,
    int ProgressPercent,
    string Currency);
