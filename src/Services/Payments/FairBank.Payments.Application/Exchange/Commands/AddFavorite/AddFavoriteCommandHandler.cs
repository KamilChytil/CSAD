using FairBank.Payments.Application.Exchange.DTOs;
using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.AddFavorite;

public sealed class AddFavoriteCommandHandler(
    IExchangeFavoriteRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<AddFavoriteCommand, ExchangeFavoriteResponse>
{
    public async Task<ExchangeFavoriteResponse> Handle(AddFavoriteCommand request, CancellationToken cancellationToken)
    {
        var favorite = ExchangeFavorite.Create(request.UserId, request.FromCurrency, request.ToCurrency);
        await repository.AddAsync(favorite, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ExchangeFavoriteResponse(favorite.Id, favorite.FromCurrency, favorite.ToCurrency, favorite.CreatedAt);
    }
}
