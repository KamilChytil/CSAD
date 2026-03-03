using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Domain.Ports;
using MediatR;

namespace FairBank.Payments.Application.Templates.Queries.GetTemplatesByAccount;

public sealed record GetTemplatesByAccountQuery(Guid AccountId) : IRequest<IReadOnlyList<PaymentTemplateResponse>>;

public sealed class GetTemplatesByAccountQueryHandler(
    IPaymentTemplateRepository repository) : IRequestHandler<GetTemplatesByAccountQuery, IReadOnlyList<PaymentTemplateResponse>>
{
    public async Task<IReadOnlyList<PaymentTemplateResponse>> Handle(GetTemplatesByAccountQuery request, CancellationToken ct)
    {
        var templates = await repository.GetByAccountIdAsync(request.AccountId, ct);

        return templates.Select(t => new PaymentTemplateResponse(
            t.Id, t.OwnerAccountId, t.Name,
            t.RecipientAccountNumber, t.RecipientName,
            t.DefaultAmount, t.Currency.ToString(),
            t.DefaultDescription, t.CreatedAt)).ToList();
    }
}
