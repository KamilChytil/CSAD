using FairBank.Products.Domain.Repositories;
using FairBank.Products.Infrastructure.Persistence;
using FairBank.Products.Infrastructure.Persistence.Repositories;
using FairBank.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Products.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ProductsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "products_service");
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            }));

        services.AddScoped<IProductApplicationRepository, ProductApplicationRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ProductsDbContext>());

        return services;
    }
}
