using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.RemoveFavorite;

public sealed class RemoveFavoriteCommandHandler(
    IExchangeFavoriteRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<RemoveFavoriteCommand, bool>
{
    public async Task<bool> Handle(RemoveFavoriteCommand request, CancellationToken cancellationToken)
    {
        var favorite = await repository.GetByIdAsync(request.FavoriteId, cancellationToken);
        if (favorite is null) return false;
        repository.Remove(favorite);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
