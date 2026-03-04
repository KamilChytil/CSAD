namespace FairBank.SharedKernel.Logging;

public interface IAuditLogger
{
    void LogSecurityEvent(string action, string outcome, Guid? userId = null, string? details = null, string? ipAddress = null);
}
