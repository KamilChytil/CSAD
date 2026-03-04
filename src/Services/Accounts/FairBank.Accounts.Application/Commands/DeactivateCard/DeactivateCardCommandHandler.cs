using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.DeactivateCard;

public sealed class DeactivateCardCommandHandler(ICardEventStore cardEventStore)
    : IRequestHandler<DeactivateCardCommand>
{
    public async Task Handle(DeactivateCardCommand request, CancellationToken ct)
    {
        var card = await cardEventStore.LoadAsync(request.CardId, ct)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.Deactivate();

        await cardEventStore.AppendEventsAsync(card, ct);
    }
}
