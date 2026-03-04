using FairBank.Payments.Application.Exchange.Services;
using FairBank.Payments.Application.Ports;
using FairBank.Payments.Domain.Ports;
using FairBank.Payments.Infrastructure.HttpClients;
using FairBank.Payments.Infrastructure.Persistence;
using FairBank.Payments.Infrastructure.Persistence.Repositories;
using FairBank.Payments.Infrastructure.Services;
using FairBank.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string accountsApiBaseUrl,
        string identityApiBaseUrl)
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
        services.AddScoped<IExchangeTransactionRepository, ExchangeTransactionRepository>();
        services.AddScoped<IExchangeFavoriteRepository, ExchangeFavoriteRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PaymentsDbContext>());

        services.AddTransient<AuthHeaderForwardingHandler>();
        services.AddHttpClient<IAccountsServiceClient, AccountsServiceHttpClient>(client =>
        {
            client.BaseAddress = new Uri(accountsApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddHttpMessageHandler<AuthHeaderForwardingHandler>();

        services.AddHttpClient<INotificationClient, NotificationHttpClient>(client =>
        {
            client.BaseAddress = new Uri(identityApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHttpClient<IIdentityClient, IdentityHttpClient>(client =>
        {
            client.BaseAddress = new Uri(identityApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddMemoryCache();
        services.AddHttpClient<IExchangeRateService, ExchangeRateService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
