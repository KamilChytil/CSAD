using MediatR;

namespace FairBank.Payments.Application.Payments.Queries.ExportPayments;

public sealed record ExportPaymentsQuery(
    Guid AccountId,
    string Format = "csv",
    DateTime? DateFrom = null,
    DateTime? DateTo = null) : IRequest<ExportResult>;

public sealed record ExportResult(byte[] Data, string ContentType, string FileName);
