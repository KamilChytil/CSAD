using FairBank.Payments.Application.Exchange.DTOs;
using FairBank.Payments.Domain.Ports;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetFavorites;

public sealed class GetFavoritesQueryHandler(IExchangeFavoriteRepository repository)
    : IRequestHandler<GetFavoritesQuery, IReadOnlyList<ExchangeFavoriteResponse>>
{
    public async Task<IReadOnlyList<ExchangeFavoriteResponse>> Handle(
        GetFavoritesQuery request, CancellationToken cancellationToken)
    {
        var favorites = await repository.GetByUserIdAsync(request.UserId, cancellationToken);
        return favorites.Select(f => new ExchangeFavoriteResponse(
            f.Id, f.FromCurrency.ToString(), f.ToCurrency.ToString(), f.CreatedAt)).ToList();
    }
}
