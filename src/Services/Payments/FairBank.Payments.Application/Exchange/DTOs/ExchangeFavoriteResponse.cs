namespace FairBank.Payments.Application.Exchange.DTOs;

public sealed record ExchangeFavoriteResponse(Guid Id, string FromCurrency, string ToCurrency, DateTime CreatedAt);
