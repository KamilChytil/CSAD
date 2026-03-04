namespace FairBank.Web.Shared.Models;

public sealed record ExchangeRateDto(decimal Rate, string FromCurrency, string ToCurrency, string RateDate);

public sealed record ExchangeTransactionDto(
    Guid Id, Guid SourceAccountId, Guid TargetAccountId,
    string FromCurrency, string ToCurrency,
    decimal SourceAmount, decimal TargetAmount,
    decimal ExchangeRate, DateTime CreatedAt);

public sealed record ExchangeFavoriteDto(Guid Id, string FromCurrency, string ToCurrency, DateTime CreatedAt);

public sealed record ExecuteExchangeRequest(
    Guid UserId, Guid SourceAccountId, Guid TargetAccountId,
    decimal Amount, string FromCurrency, string ToCurrency);

public sealed record AddFavoriteRequest(Guid UserId, string FromCurrency, string ToCurrency);
