using FairBank.Payments.Application.Exchange.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetFavorites;

public sealed record GetFavoritesQuery(Guid UserId) : IRequest<IReadOnlyList<ExchangeFavoriteResponse>>;
