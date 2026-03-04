using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.UnfreezeCard;

public sealed class UnfreezeCardCommandHandler(ICardEventStore cardEventStore)
    : IRequestHandler<UnfreezeCardCommand>
{
    public async Task Handle(UnfreezeCardCommand request, CancellationToken ct)
    {
        var card = await cardEventStore.LoadAsync(request.CardId, ct)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.Unfreeze();

        await cardEventStore.AppendEventsAsync(card, ct);
    }
}
