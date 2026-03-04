using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetInvestmentById;

public sealed class GetInvestmentByIdQueryHandler(IInvestmentEventStore investmentEventStore)
    : IRequestHandler<GetInvestmentByIdQuery, InvestmentResponse?>
{
    public async Task<InvestmentResponse?> Handle(GetInvestmentByIdQuery request, CancellationToken ct)
    {
        var investment = await investmentEventStore.LoadAsync(request.InvestmentId, ct);

        if (investment is null) return null;

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
