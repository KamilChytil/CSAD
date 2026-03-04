using FairBank.Payments.Application.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Payments.Queries.GetPaymentStatistics;

public sealed record GetPaymentStatisticsQuery(
    Guid AccountId,
    string Period = "monthly",
    DateTime? DateFrom = null,
    DateTime? DateTo = null) : IRequest<PaymentStatisticsResponse>;
