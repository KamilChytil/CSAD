using Microsoft.Extensions.Logging;

namespace FairBank.SharedKernel.Logging;

public class SerilogAuditLogger : IAuditLogger
{
    private readonly ILogger<SerilogAuditLogger> _logger;

    public SerilogAuditLogger(ILogger<SerilogAuditLogger> logger) => _logger = logger;

    public void LogSecurityEvent(string action, string outcome, Guid? userId = null, string? details = null, string? ipAddress = null)
    {
        _logger.LogInformation(
            "AUDIT | Action={Action} Outcome={Outcome} UserId={UserId} IP={IpAddress} Details={Details}",
            action, outcome, userId, ipAddress, details);
    }
}
