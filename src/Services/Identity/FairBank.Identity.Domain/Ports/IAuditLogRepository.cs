using FairBank.Identity.Domain.Entities;
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Ports;

public interface IAuditLogRepository : IRepository<AuditLog, Guid>
{
    Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct);
}
