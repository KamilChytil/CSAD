using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Application.DTOs;

public sealed record PendingTransactionResponse(
    Guid Id,
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description,
    Guid RequestedBy,
    PendingTransactionStatus Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt);
