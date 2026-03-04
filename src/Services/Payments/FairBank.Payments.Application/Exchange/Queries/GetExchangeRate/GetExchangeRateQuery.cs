using FairBank.Payments.Application.Exchange.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetExchangeRate;

public sealed record GetExchangeRateQuery(string FromCurrency, string ToCurrency) : IRequest<ExchangeRateResponse?>;
