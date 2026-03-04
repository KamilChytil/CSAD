using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetInvestmentsByAccount;

public sealed class GetInvestmentsByAccountQueryHandler(IInvestmentEventStore investmentEventStore)
    : IRequestHandler<GetInvestmentsByAccountQuery, IReadOnlyList<InvestmentResponse>>
{
    public async Task<IReadOnlyList<InvestmentResponse>> Handle(GetInvestmentsByAccountQuery request, CancellationToken ct)
    {
        var investments = await investmentEventStore.LoadByAccountAsync(request.AccountId, ct);

        return investments
            .Select(MapToResponse)
            .ToList();
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
