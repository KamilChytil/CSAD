using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.UpdateCardSettings;

public sealed class UpdateCardSettingsCommandHandler(ICardEventStore cardEventStore)
    : IRequestHandler<UpdateCardSettingsCommand>
{
    public async Task Handle(UpdateCardSettingsCommand request, CancellationToken ct)
    {
        var card = await cardEventStore.LoadAsync(request.CardId, ct)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.UpdateSettings(request.OnlinePaymentsEnabled, request.ContactlessEnabled);

        await cardEventStore.AppendEventsAsync(card, ct);
    }
}
