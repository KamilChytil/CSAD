namespace FairBank.Payments.Application.DTOs;

public sealed record PaymentTemplateResponse(
    Guid Id,
    Guid OwnerAccountId,
    string Name,
    string RecipientAccountNumber,
    string? RecipientName,
    decimal? DefaultAmount,
    string Currency,
    string? DefaultDescription,
    DateTime CreatedAt);
