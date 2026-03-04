using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Domain.Ports;
using MediatR;

namespace FairBank.Payments.Application.Payments.Queries.GetPaymentsByAccount;

public sealed record GetPaymentsByAccountQuery(Guid AccountId, int Limit = 50) : IRequest<IReadOnlyList<PaymentResponse>>;

public sealed class GetPaymentsByAccountQueryHandler(
    IPaymentRepository paymentRepository) : IRequestHandler<GetPaymentsByAccountQuery, IReadOnlyList<PaymentResponse>>
{
    public async Task<IReadOnlyList<PaymentResponse>> Handle(GetPaymentsByAccountQuery request, CancellationToken ct)
    {
        var payments = await paymentRepository.GetByAccountIdAsync(request.AccountId, request.Limit, ct);

        return payments.Select(p => new PaymentResponse(
            p.Id, p.SenderAccountId, p.RecipientAccountId,
            p.SenderAccountNumber, p.RecipientAccountNumber,
            p.Amount, p.Currency.ToString(), p.Description,
            p.Type.ToString(), p.Status.ToString(),
            p.Category.ToString(),
            p.CreatedAt, p.CompletedAt, p.FailureReason)).ToList();
    }
}
