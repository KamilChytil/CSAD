namespace FairBank.Web.Shared.Models;

public sealed record ProductApplicationDto(
    Guid Id,
    Guid UserId,
    string ProductType,
    string Status,
    string Parameters,
    decimal MonthlyPayment,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    Guid? ReviewedBy,
    string? Note);
