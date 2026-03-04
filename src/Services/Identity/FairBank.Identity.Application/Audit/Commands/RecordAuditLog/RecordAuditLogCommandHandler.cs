using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Audit.Commands.RecordAuditLog;

public sealed record RecordAuditLogCommand(
    string Action,
    Guid? UserId = null,
    string? UserEmail = null,
    string? EntityName = null,
    string? EntityId = null,
    string? Details = null,
    string? IpAddress = null) : IRequest;

public sealed class RecordAuditLogCommandHandler(
    IAuditLogRepository repo,
    IUnitOfWork uow) : IRequestHandler<RecordAuditLogCommand>
{
    public async Task Handle(RecordAuditLogCommand request, CancellationToken ct)
    {
        var log = AuditLog.Create(
            request.Action,
            request.UserId,
            request.UserEmail,
            request.EntityName,
            request.EntityId,
            request.Details,
            request.IpAddress);

        await repo.AddAsync(log, ct);
        await uow.SaveChangesAsync(ct);
    }
}
