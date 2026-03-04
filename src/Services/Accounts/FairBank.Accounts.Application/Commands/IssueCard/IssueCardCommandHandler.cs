using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using MediatR;

namespace FairBank.Accounts.Application.Commands.IssueCard;

public sealed class IssueCardCommandHandler(IAccountEventStore accountEventStore, ICardEventStore cardEventStore)
    : IRequestHandler<IssueCardCommand, CardResponse>
{
    public async Task<CardResponse> Handle(IssueCardCommand request, CancellationToken ct)
    {
        var account = await accountEventStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException($"Account {request.AccountId} not found.");

        var card = Card.Create(account.Id, request.HolderName, request.Type, account.Balance.Currency);

        await cardEventStore.StartStreamAsync(card, ct);

        return MapToResponse(card);
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
