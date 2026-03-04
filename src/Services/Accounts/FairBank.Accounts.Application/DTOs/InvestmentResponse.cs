using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Application.DTOs;

public sealed record InvestmentResponse(
    Guid Id,
    Guid AccountId,
    string Name,
    InvestmentType Type,
    decimal InvestedAmount,
    decimal CurrentValue,
    decimal Units,
    decimal PricePerUnit,
    decimal ChangePercent,
    string Currency,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? SoldAt);
