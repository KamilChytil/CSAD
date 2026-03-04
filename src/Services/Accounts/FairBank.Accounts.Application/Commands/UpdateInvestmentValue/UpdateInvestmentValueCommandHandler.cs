using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.UpdateInvestmentValue;

public sealed class UpdateInvestmentValueCommandHandler(IInvestmentEventStore investmentEventStore)
    : IRequestHandler<UpdateInvestmentValueCommand>
{
    public async Task Handle(UpdateInvestmentValueCommand request, CancellationToken ct)
    {
        var investment = await investmentEventStore.LoadAsync(request.InvestmentId, ct)
            ?? throw new InvalidOperationException($"Investment {request.InvestmentId} not found.");

        investment.UpdateValue(request.NewPricePerUnit);

        await investmentEventStore.AppendEventsAsync(investment, ct);
    }
}
