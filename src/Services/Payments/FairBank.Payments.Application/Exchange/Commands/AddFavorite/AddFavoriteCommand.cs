using FairBank.Payments.Application.Exchange.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.AddFavorite;

public sealed record AddFavoriteCommand(Guid UserId, string FromCurrency, string ToCurrency) : IRequest<ExchangeFavoriteResponse>;
