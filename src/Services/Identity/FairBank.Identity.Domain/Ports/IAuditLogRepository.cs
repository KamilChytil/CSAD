using FairBank.Identity.Domain.Entities;
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Ports;

public interface IAuditLogRepository : IRepository<AuditLog, Guid>
{
    Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Guid? userId,
        string? action,
        string? entityName,
        string? details,
        DateTime? startDate,
        DateTime? endDate,
        string sortBy = "Timestamp",
        bool sortDesc = true,
        CancellationToken ct = default);
}
