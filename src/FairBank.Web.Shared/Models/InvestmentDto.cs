namespace FairBank.Web.Shared.Models;

public sealed record InvestmentDto(
    Guid Id,
    Guid AccountId,
    string Name,
    string Type,
    decimal InvestedAmount,
    decimal CurrentValue,
    decimal Units,
    decimal PricePerUnit,
    decimal ChangePercent,
    string Currency,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? SoldAt);
