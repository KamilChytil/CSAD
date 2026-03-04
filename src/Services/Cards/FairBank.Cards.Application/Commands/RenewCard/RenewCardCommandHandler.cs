using FairBank.Cards.Application.DTOs;
using FairBank.Cards.Domain.Aggregates;
using FairBank.Cards.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Cards.Application.Commands.RenewCard;

public sealed class RenewCardCommandHandler(
    ICardRepository cardRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RenewCardCommand, CardResponse>
{
    public async Task<CardResponse> Handle(RenewCardCommand request, CancellationToken ct)
    {
        var card = await cardRepository.GetByIdAsync(request.CardId, ct)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        var newCard = card.Renew();

        await cardRepository.UpdateAsync(card, ct);
        await cardRepository.AddAsync(newCard, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(newCard);
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
