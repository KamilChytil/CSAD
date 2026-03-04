using FairBank.Payments.Application.Exchange.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.ExecuteExchange;

public sealed record ExecuteExchangeCommand(
    Guid UserId, Guid SourceAccountId, Guid TargetAccountId,
    decimal Amount, string FromCurrency, string ToCurrency) : IRequest<ExchangeTransactionResponse>;
