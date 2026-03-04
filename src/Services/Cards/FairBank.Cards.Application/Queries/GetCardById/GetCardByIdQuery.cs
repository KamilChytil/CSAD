using FairBank.Cards.Application.DTOs;
using FairBank.Cards.Domain.Aggregates;
using FairBank.Cards.Domain.Ports;
using MediatR;

namespace FairBank.Cards.Application.Queries.GetCardById;

public sealed record GetCardByIdQuery(Guid Id) : IRequest<CardResponse?>;

public sealed class GetCardByIdQueryHandler(
    ICardRepository cardRepository) : IRequestHandler<GetCardByIdQuery, CardResponse?>
{
    public async Task<CardResponse?> Handle(GetCardByIdQuery request, CancellationToken ct)
    {
        var card = await cardRepository.GetByIdAsync(request.Id, ct);
        if (card is null)
            return null;

        return MapToResponse(card);
    }

    private static CardResponse MapToResponse(Card c) => new(
        c.Id, c.AccountId, c.UserId,
        c.CardNumber.MaskedNumber, c.CardNumber.LastFourDigits,
        c.CardholderName, c.ExpirationDate,
        c.CardType.ToString(), c.CardBrand.ToString(),
        c.Status.ToString(), c.DailyLimit, c.MonthlyLimit,
        c.OnlinePaymentsEnabled, c.ContactlessEnabled,
        c.PinHash is not null, c.CreatedAt);
}
