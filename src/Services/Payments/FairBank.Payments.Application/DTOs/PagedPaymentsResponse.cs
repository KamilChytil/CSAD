namespace FairBank.Payments.Application.DTOs;

public sealed record PagedPaymentsResponse(
    IReadOnlyList<PaymentResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
