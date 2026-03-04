using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.SellInvestment;

public sealed class SellInvestmentCommandHandler(IInvestmentEventStore investmentEventStore)
    : IRequestHandler<SellInvestmentCommand>
{
    public async Task Handle(SellInvestmentCommand request, CancellationToken ct)
    {
        var investment = await investmentEventStore.LoadAsync(request.InvestmentId, ct)
            ?? throw new InvalidOperationException($"Investment {request.InvestmentId} not found.");

        investment.Sell();

        await investmentEventStore.AppendEventsAsync(investment, ct);
    }
}
