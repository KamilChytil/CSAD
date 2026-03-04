using FairBank.Documents.Application.Ports;
using FairBank.Documents.Infrastructure.HttpClients;
using FairBank.Documents.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Documents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentsInfrastructure(this IServiceCollection services, string accountsApiUrl)
    {
        services.AddHttpClient<IAccountsServiceClient, AccountsServiceHttpClient>(c =>
        {
            c.BaseAddress = new Uri(accountsApiUrl);
        });
        services.AddSingleton<IStatementGenerator, StatementGenerator>();
        return services;
    }
}
