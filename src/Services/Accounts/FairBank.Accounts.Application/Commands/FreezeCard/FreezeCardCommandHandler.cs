using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.FreezeCard;

public sealed class FreezeCardCommandHandler(ICardEventStore cardEventStore)
    : IRequestHandler<FreezeCardCommand>
{
    public async Task Handle(FreezeCardCommand request, CancellationToken ct)
    {
        var card = await cardEventStore.LoadAsync(request.CardId, ct)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.Freeze();

        await cardEventStore.AppendEventsAsync(card, ct);
    }
}
