using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Domain.Enums;
using MediatR;

namespace FairBank.Payments.Application.Payments.Queries.SearchPayments;

public sealed record SearchPaymentsQuery(
    Guid AccountId,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    PaymentCategory? Category = null,
    PaymentStatus? Status = null,
    string? SearchText = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "CreatedAt",
    string SortDirection = "desc") : IRequest<PagedPaymentsResponse>;
