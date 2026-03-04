namespace FairBank.Products.Application.Dtos;

public sealed record ProductApplicationResponse(
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
