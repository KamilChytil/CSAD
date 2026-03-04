using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetCardsByAccount;

public sealed class GetCardsByAccountQueryHandler(ICardEventStore cardEventStore)
    : IRequestHandler<GetCardsByAccountQuery, IReadOnlyList<CardResponse>>
{
    public async Task<IReadOnlyList<CardResponse>> Handle(GetCardsByAccountQuery request, CancellationToken ct)
    {
        var cards = await cardEventStore.LoadByAccountAsync(request.AccountId, ct);

        return cards
            .Select(MapToResponse)
            .ToList();
    }

    private static CardResponse MapToResponse(Card card) => new(
        card.Id,
        card.AccountId,
        card.MaskedNumber,
        card.HolderName,
        card.ExpirationDate,
        card.Type,
        card.IsActive,
        card.IsFrozen,
        card.DailyLimit?.Amount,
        card.MonthlyLimit?.Amount,
        card.OnlinePaymentsEnabled,
        card.ContactlessEnabled,
        card.CreatedAt);
}
