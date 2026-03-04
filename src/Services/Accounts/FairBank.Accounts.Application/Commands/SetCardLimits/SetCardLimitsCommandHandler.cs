using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.SetCardLimits;

public sealed class SetCardLimitsCommandHandler(ICardEventStore cardEventStore)
    : IRequestHandler<SetCardLimitsCommand>
{
    public async Task Handle(SetCardLimitsCommand request, CancellationToken ct)
    {
        var card = await cardEventStore.LoadAsync(request.CardId, ct)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");

        card.SetLimits(request.DailyLimit, request.MonthlyLimit, request.Currency);

        await cardEventStore.AppendEventsAsync(card, ct);
    }
}
