using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Application.DTOs;

public sealed record CardResponse(
    Guid Id,
    Guid AccountId,
    string MaskedNumber,
    string HolderName,
    DateTime ExpirationDate,
    CardType Type,
    bool IsActive,
    bool IsFrozen,
    decimal? DailyLimit,
    decimal? MonthlyLimit,
    bool OnlinePaymentsEnabled,
    bool ContactlessEnabled,
    DateTime CreatedAt);
