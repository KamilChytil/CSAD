using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreateInvestment;

public sealed class CreateInvestmentCommandHandler(IInvestmentEventStore investmentEventStore)
    : IRequestHandler<CreateInvestmentCommand, InvestmentResponse>
{
    public async Task<InvestmentResponse> Handle(CreateInvestmentCommand request, CancellationToken ct)
    {
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
