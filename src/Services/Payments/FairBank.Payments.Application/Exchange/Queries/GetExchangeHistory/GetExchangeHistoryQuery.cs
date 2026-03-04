using FairBank.Payments.Application.Exchange.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetExchangeHistory;

public sealed record GetExchangeHistoryQuery(Guid UserId, int Limit = 20) : IRequest<IReadOnlyList<ExchangeTransactionResponse>>;
