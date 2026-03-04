namespace FairBank.Web.Shared.Models;

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
