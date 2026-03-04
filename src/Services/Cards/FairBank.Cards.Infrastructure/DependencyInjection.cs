using FairBank.Cards.Domain.Ports;
using FairBank.Cards.Infrastructure.Persistence;
using FairBank.Cards.Infrastructure.Persistence.Repositories;
using FairBank.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Cards.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCardsInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CardsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "cards_service");
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            }));

        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CardsDbContext>());

        return services;
    }
}
