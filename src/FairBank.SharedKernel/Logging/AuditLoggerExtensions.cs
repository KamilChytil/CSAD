using Microsoft.Extensions.DependencyInjection;

namespace FairBank.SharedKernel.Logging;

public static class AuditLoggerExtensions
{
    public static IServiceCollection AddAuditLogging(this IServiceCollection services)
    {
        services.AddSingleton<IAuditLogger, SerilogAuditLogger>();
        return services;
    }
}
