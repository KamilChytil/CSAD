using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Documents.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentsApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
