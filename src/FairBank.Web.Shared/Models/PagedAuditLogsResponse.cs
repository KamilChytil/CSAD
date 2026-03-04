namespace FairBank.Web.Shared.Models;

public sealed record PagedAuditLogsResponse(
    IEnumerable<AuditLogResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
