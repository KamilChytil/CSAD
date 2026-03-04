using FairBank.Payments.Application.Exchange.DTOs;
using FairBank.Payments.Domain.Ports;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetExchangeHistory;

public sealed class GetExchangeHistoryQueryHandler(IExchangeTransactionRepository repository)
    : IRequestHandler<GetExchangeHistoryQuery, IReadOnlyList<ExchangeTransactionResponse>>
{
    public async Task<IReadOnlyList<ExchangeTransactionResponse>> Handle(
        GetExchangeHistoryQuery request, CancellationToken cancellationToken)
    {
        var transactions = await repository.GetByUserIdAsync(request.UserId, request.Limit, cancellationToken);
        return transactions.Select(t => new ExchangeTransactionResponse(
            t.Id, t.SourceAccountId, t.TargetAccountId,
            t.FromCurrency.ToString(), t.ToCurrency.ToString(),
            t.SourceAmount, t.TargetAmount, t.ExchangeRate, t.CreatedAt)).ToList();
    }
}
