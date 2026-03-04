using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.RemoveFavorite;

public sealed record RemoveFavoriteCommand(Guid FavoriteId) : IRequest<bool>;
