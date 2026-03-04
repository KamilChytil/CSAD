using FairBank.Cards.Application.DTOs;
using FairBank.Cards.Domain.Aggregates;
using FairBank.Cards.Domain.Ports;
using MediatR;

namespace FairBank.Cards.Application.Queries.GetCardsByAccount;

public sealed record GetCardsByAccountQuery(Guid AccountId) : IRequest<IReadOnlyList<CardResponse>>;

public sealed class GetCardsByAccountQueryHandler(
    ICardRepository cardRepository) : IRequestHandler<GetCardsByAccountQuery, IReadOnlyList<CardResponse>>
{
    public async Task<IReadOnlyList<CardResponse>> Handle(GetCardsByAccountQuery request, CancellationToken ct)
    {
        var cards = await cardRepository.GetByAccountIdAsync(request.AccountId, ct);

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
