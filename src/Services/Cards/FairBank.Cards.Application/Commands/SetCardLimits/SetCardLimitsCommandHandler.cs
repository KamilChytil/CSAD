using FairBank.Cards.Application.DTOs;
using FairBank.Cards.Domain.Aggregates;
using FairBank.Cards.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Cards.Application.Commands.SetCardLimits;

public sealed class SetCardLimitsCommandHandler(
    ICardRepository cardRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<SetCardLimitsCommand, CardResponse>
{
    public async Task<CardResponse> Handle(SetCardLimitsCommand request, CancellationToken ct)
    {
        var card = await cardRepository.GetByIdAsync(request.CardId, ct)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.SetLimits(request.DailyLimit, request.MonthlyLimit);

        await cardRepository.UpdateAsync(card, ct);
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
