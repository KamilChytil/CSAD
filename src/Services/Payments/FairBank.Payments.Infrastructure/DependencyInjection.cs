using FairBank.Payments.Application.Ports;
using FairBank.Payments.Domain.Ports;
using FairBank.Payments.Infrastructure.HttpClients;
using FairBank.Payments.Infrastructure.Persistence;
using FairBank.Payments.Infrastructure.Persistence.Repositories;
using FairBank.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string accountsApiBaseUrl)
    {
        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "payments_service");
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            }));

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IStandingOrderRepository, StandingOrderRepository>();
        services.AddScoped<IPaymentTemplateRepository, PaymentTemplateRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PaymentsDbContext>());

        services.AddHttpClient<IAccountsServiceClient, AccountsServiceHttpClient>(client =>
        {
            client.BaseAddress = new Uri(accountsApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
