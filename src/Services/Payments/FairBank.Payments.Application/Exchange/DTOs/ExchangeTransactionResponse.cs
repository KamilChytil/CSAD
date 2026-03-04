namespace FairBank.Payments.Application.Exchange.DTOs;

public sealed record ExchangeTransactionResponse(
    Guid Id, Guid SourceAccountId, Guid TargetAccountId,
    string FromCurrency, string ToCurrency,
    decimal SourceAmount, decimal TargetAmount,
    decimal ExchangeRate, DateTime CreatedAt);
