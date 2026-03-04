using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Domain.Ports;
using MediatR;

namespace FairBank.Payments.Application.Payments.Queries.SearchPayments;

public sealed class SearchPaymentsQueryHandler(
    IPaymentRepository paymentRepository) : IRequestHandler<SearchPaymentsQuery, PagedPaymentsResponse>
{
    public async Task<PagedPaymentsResponse> Handle(SearchPaymentsQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await paymentRepository.SearchAsync(
            request.AccountId,
            request.DateFrom,
            request.DateTo,
            request.MinAmount,
            request.MaxAmount,
            request.Category,
            request.Status,
            request.SearchText,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.SortDirection,
            ct);

        var responses = items.Select(p => new PaymentResponse(
            p.Id, p.SenderAccountId, p.RecipientAccountId,
            p.SenderAccountNumber, p.RecipientAccountNumber,
            p.Amount, p.Currency.ToString(), p.Description,
            p.Type.ToString(), p.Status.ToString(),
            p.Category.ToString(),
            p.CreatedAt, p.CompletedAt, p.FailureReason)).ToList();

        return new PagedPaymentsResponse(responses, totalCount, request.Page, request.PageSize);
    }
}
