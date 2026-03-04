using System.Text;
using FairBank.Payments.Domain.Ports;
using MediatR;

namespace FairBank.Payments.Application.Payments.Queries.ExportPayments;

public sealed class ExportPaymentsQueryHandler(
    IPaymentRepository paymentRepository) : IRequestHandler<ExportPaymentsQuery, ExportResult>
{
    public async Task<ExportResult> Handle(ExportPaymentsQuery request, CancellationToken ct)
    {
        var (payments, _) = await paymentRepository.SearchAsync(
            request.AccountId,
            request.DateFrom,
            request.DateTo,
            minAmount: null,
            maxAmount: null,
            category: null,
            status: null,
            searchText: null,
            page: 1,
            pageSize: int.MaxValue,
            sortBy: "CreatedAt",
            sortDirection: "desc",
            ct);

        var sb = new StringBuilder();
        sb.AppendLine("Datum;Popis;Částka;Měna;Typ;Kategorie;Status");
        foreach (var p in payments)
        {
            sb.AppendLine($"{p.CreatedAt:yyyy-MM-dd};{p.Description};{p.Amount};{p.Currency};{p.Type};{p.Category};{p.Status}");
        }

        return new ExportResult(
            Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            $"transactions_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
