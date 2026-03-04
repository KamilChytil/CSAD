namespace FairBank.Web.Shared.Models;

public sealed record CardDto(
    Guid Id,
    Guid AccountId,
    string MaskedNumber,
    string HolderName,
    DateTime ExpirationDate,
    string Type,
    bool IsActive,
    bool IsFrozen,
    decimal? DailyLimit,
    decimal? MonthlyLimit,
    bool OnlinePaymentsEnabled,
    bool ContactlessEnabled,
    DateTime CreatedAt);
