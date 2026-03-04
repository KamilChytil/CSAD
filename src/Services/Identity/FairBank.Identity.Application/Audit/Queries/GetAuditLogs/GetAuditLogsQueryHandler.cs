using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Audit.Queries.GetAuditLogs;

public sealed record GetAuditLogsQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? UserId = null,
    string? Action = null,
    string? EntityName = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null) : IRequest<PagedAuditLogsResponse>;

public sealed record PagedAuditLogsResponse(
    IEnumerable<AuditLogResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AuditLogResponse(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string Action,
    string? EntityName,
    string? EntityId,
    string? Details,
    string? IpAddress,
    DateTime Timestamp);

public sealed class GetAuditLogsQueryHandler(IAuditLogRepository repo) : IRequestHandler<GetAuditLogsQuery, PagedAuditLogsResponse>
{
    public async Task<PagedAuditLogsResponse> Handle(GetAuditLogsQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await repo.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.UserId,
            request.Action,
            request.EntityName,
            request.StartDate,
            request.EndDate,
            ct);

        var dtos = items.Select(l => new AuditLogResponse(
            l.Id,
            l.UserId,
            l.UserEmail,
            l.Action,
            l.EntityName,
            l.EntityId,
            l.Details,
            l.IpAddress,
            l.Timestamp));

        return new PagedAuditLogsResponse(dtos, totalCount, request.Page, request.PageSize);
    }
}
