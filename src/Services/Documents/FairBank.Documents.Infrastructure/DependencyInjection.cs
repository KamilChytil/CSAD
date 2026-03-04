using FairBank.Documents.Application.Ports;
using FairBank.Documents.Infrastructure.HttpClients;
using FairBank.Documents.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Documents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentsInfrastructure(this IServiceCollection services, string accountsApiUrl, string? productsApiUrl = null)
    {
        services.AddHttpClient<IAccountsServiceClient, AccountsServiceHttpClient>(c =>
        {
            c.BaseAddress = new Uri(accountsApiUrl);
        });
        services.AddHttpClient<IProductsServiceClient, ProductsServiceHttpClient>(c =>
        {
            c.BaseAddress = new Uri(productsApiUrl ?? accountsApiUrl); // fallback so it doesn't crash if not set
        });
        services.AddSingleton<IStatementGenerator, StatementGenerator>();
        services.AddSingleton<IContractGenerator, ContractGenerator>();
        return services;
    }
}
