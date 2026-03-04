using FairBank.Cards.Application.DTOs;
using FairBank.Cards.Domain.Aggregates;
using FairBank.Cards.Domain.Ports;
using MediatR;

namespace FairBank.Cards.Application.Queries.GetCardsByUser;

public sealed record GetCardsByUserQuery(Guid UserId) : IRequest<IReadOnlyList<CardResponse>>;

public sealed class GetCardsByUserQueryHandler(
    ICardRepository cardRepository) : IRequestHandler<GetCardsByUserQuery, IReadOnlyList<CardResponse>>
{
    public async Task<IReadOnlyList<CardResponse>> Handle(GetCardsByUserQuery request, CancellationToken ct)
    {
        var cards = await cardRepository.GetByUserIdAsync(request.UserId, ct);

        if (cards is null || cards.Count == 0)
            return Array.Empty<CardResponse>();

        return cards.Select(c => MapToResponse(c)).ToList();
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
