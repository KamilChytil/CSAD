namespace FairBank.Web.Shared.Models;

public sealed record TransactionDto(
    Guid Id,
    string Description,
    DateTime Date,
    decimal Amount,
    string Type);
