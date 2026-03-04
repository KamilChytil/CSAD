using FairBank.Payments.Application.Exchange.DTOs;
using FairBank.Payments.Application.Exchange.Services;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetExchangeRate;

public sealed class GetExchangeRateQueryHandler(IExchangeRateService exchangeRateService)
    : IRequestHandler<GetExchangeRateQuery, ExchangeRateResponse?>
{
    public async Task<ExchangeRateResponse?> Handle(GetExchangeRateQuery request, CancellationToken cancellationToken)
    {
        var result = await exchangeRateService.GetRateAsync(request.FromCurrency, request.ToCurrency, cancellationToken);
        if (result is null) return null;
        return new ExchangeRateResponse(result.Rate, result.FromCurrency, result.ToCurrency, result.RateDate);
    }
}
