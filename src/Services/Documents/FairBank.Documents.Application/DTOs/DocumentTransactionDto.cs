namespace FairBank.Documents.Application.DTOs;

public sealed record DocumentTransactionDto(
    DateTime OccurredAt,
    string Type,
    decimal Amount,
    string Currency,
    string Description);
