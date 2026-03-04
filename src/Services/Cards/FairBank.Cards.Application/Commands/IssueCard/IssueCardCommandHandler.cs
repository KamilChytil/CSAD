using FairBank.Cards.Application.DTOs;
using FairBank.Cards.Domain.Aggregates;
using FairBank.Cards.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Cards.Application.Commands.IssueCard;

public sealed class IssueCardCommandHandler(
    ICardRepository cardRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<IssueCardCommand, CardResponse>
{
    public async Task<CardResponse> Handle(IssueCardCommand request, CancellationToken ct)
    {
        var card = Card.Issue(
            request.AccountId,
            request.UserId,
            request.CardholderName,
            request.CardType,
            request.CardBrand);

        await cardRepository.AddAsync(card, ct);
        await unitOfWork.SaveChangesAsync(ct);

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
