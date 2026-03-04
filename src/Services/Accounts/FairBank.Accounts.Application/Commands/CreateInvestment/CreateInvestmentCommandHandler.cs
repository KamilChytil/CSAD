using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreateInvestment;

public sealed class CreateInvestmentCommandHandler(
    IInvestmentEventStore investmentEventStore,
    IAccountEventStore accountEventStore)
    : IRequestHandler<CreateInvestmentCommand, InvestmentResponse>
{
    public async Task<InvestmentResponse> Handle(CreateInvestmentCommand request, CancellationToken ct)
    {
        // Withdraw the invested amount from the source account
        var account = await accountEventStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException($"Account {request.AccountId} not found.");

        account.Withdraw(
            Money.Create(request.Amount, request.Currency),
            $"Investice: {request.Name}");

        await accountEventStore.AppendEventsAsync(account, ct);

        var investment = Investment.Create(
            request.AccountId,
            request.Name,
            request.Type,
            request.Amount,
            request.Units,
            request.PricePerUnit,
            request.Currency);

        await investmentEventStore.StartStreamAsync(investment, ct);

        return MapToResponse(investment);
    }

    private static InvestmentResponse MapToResponse(Investment inv) => new(
        inv.Id,
        inv.AccountId,
        inv.Name,
        inv.Type,
        inv.InvestedAmount.Amount,
        inv.CurrentValue.Amount,
        inv.Units,
        inv.PricePerUnit,
        inv.ChangePercent,
        inv.InvestedAmount.Currency.ToString(),
        inv.IsActive,
        inv.CreatedAt,
        inv.SoldAt);
}
