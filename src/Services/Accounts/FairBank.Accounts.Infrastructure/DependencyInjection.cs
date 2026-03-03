using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Infrastructure.Persistence;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using JasperFx;

namespace FairBank.Accounts.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddMarten(options =>
        {
            options.Connection(connectionString);
            options.DatabaseSchemaName = "accounts_service";
            options.Events.DatabaseSchemaName = "accounts_service";

            // Auto-create schema in development
            options.AutoCreateSchemaObjects = AutoCreate.All;

            // Register aggregates for event sourcing
            options.Projections.Snapshot<Account>(SnapshotLifecycle.Inline);
            options.Projections.Snapshot<PendingTransaction>(SnapshotLifecycle.Inline);
        })
        .UseLightweightSessions();

        services.AddScoped<IAccountEventStore, MartenAccountEventStore>();
        services.AddScoped<IPendingTransactionStore, MartenPendingTransactionStore>();

        return services;
    }
}
