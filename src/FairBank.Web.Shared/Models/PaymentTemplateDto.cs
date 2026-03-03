namespace FairBank.Web.Shared.Models;

public sealed record PaymentTemplateDto(
    Guid Id,
    Guid OwnerAccountId,
    string Name,
    string RecipientAccountNumber,
    string? RecipientName,
    decimal? DefaultAmount,
    string Currency,
    string? DefaultDescription,
    DateTime CreatedAt);
