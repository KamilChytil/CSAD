namespace FairBank.Web.Shared.Models;

public sealed record AccountResponse(
    Guid Id,
    Guid OwnerId,
    string AccountNumber,
    decimal Balance,
    string Currency,
    bool IsActive,
    DateTime CreatedAt);
