namespace FairBank.Web.Shared.Models;

public sealed record InvestmentDto(
    Guid Id,
    string Name,
    string Type,
    decimal CurrentValue,
    decimal InvestedAmount,
    decimal ChangePercent,
    string Currency);
