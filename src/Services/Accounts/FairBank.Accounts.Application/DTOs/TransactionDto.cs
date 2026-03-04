namespace FairBank.Accounts.Application.DTOs;

public sealed record TransactionDto(
    DateTime OccurredAt,
    string Type,    // "Deposit" or "Withdrawal"
    decimal Amount,
    string Currency,
    string Description);
