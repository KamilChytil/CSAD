using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.SellInvestment;

public sealed class SellInvestmentCommandHandler(
    IInvestmentEventStore investmentEventStore,
    IAccountEventStore accountEventStore)
    : IRequestHandler<SellInvestmentCommand>
{
    public async Task Handle(SellInvestmentCommand request, CancellationToken ct)
    {
        var investment = await investmentEventStore.LoadAsync(request.InvestmentId, ct)
            ?? throw new InvalidOperationException($"Investment {request.InvestmentId} not found.");

        investment.Sell();
        await investmentEventStore.AppendEventsAsync(investment, ct);

        // Credit the sold amount back to the source account
        var account = await accountEventStore.LoadAsync(investment.AccountId, ct)
            ?? throw new InvalidOperationException($"Account {investment.AccountId} not found.");

        account.Deposit(
            Money.Create(investment.CurrentValue.Amount, investment.CurrentValue.Currency),
            $"Prodej investice: {investment.Name}");

        await accountEventStore.AppendEventsAsync(account, ct);
    }
}
