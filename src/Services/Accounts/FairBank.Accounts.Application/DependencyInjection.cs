using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Accounts.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountsApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
