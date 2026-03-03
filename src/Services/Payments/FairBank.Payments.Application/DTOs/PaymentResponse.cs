namespace FairBank.Payments.Application.DTOs;

public sealed record PaymentResponse(
    Guid Id,
    Guid SenderAccountId,
    Guid? RecipientAccountId,
    string SenderAccountNumber,
    string RecipientAccountNumber,
    decimal Amount,
    string Currency,
    string? Description,
    string Type,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? FailureReason);
