namespace FairBank.Payments.Application.Exchange.DTOs;

public sealed record ExchangeRateResponse(decimal Rate, string FromCurrency, string ToCurrency, string RateDate);
