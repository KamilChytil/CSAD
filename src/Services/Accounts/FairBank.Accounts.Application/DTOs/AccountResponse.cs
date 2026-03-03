using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Application.DTOs;

public sealed record AccountResponse(
    Guid Id,
    Guid OwnerId,
    string AccountNumber,
    decimal Balance,
    Currency Currency,
    bool IsActive,
    DateTime CreatedAt);
