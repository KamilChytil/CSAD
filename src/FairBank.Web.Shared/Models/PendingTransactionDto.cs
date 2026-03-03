namespace FairBank.Web.Shared.Models;

public sealed record PendingTransactionDto(
    Guid Id,
    Guid AccountId,
    decimal Amount,
    string Currency,
    string Description,
    Guid RequestedBy,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt);
