using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Infrastructure.HttpClients;
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
        string connectionString,
        string identityApiBaseUrl)
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
            options.Projections.Snapshot<Card>(SnapshotLifecycle.Inline);
            options.Projections.Snapshot<PendingTransaction>(SnapshotLifecycle.Inline);
            options.Projections.Snapshot<SavingsGoal>(SnapshotLifecycle.Inline);
            options.Projections.Snapshot<SavingsRule>(SnapshotLifecycle.Inline);
            options.Projections.Snapshot<Investment>(SnapshotLifecycle.Inline);
        })
        .UseLightweightSessions();

        services.AddScoped<IAccountEventStore, MartenAccountEventStore>();
        services.AddScoped<ICardEventStore, MartenCardEventStore>();
        services.AddScoped<IPendingTransactionStore, MartenPendingTransactionStore>();
        services.AddScoped<ISavingsGoalEventStore, MartenSavingsGoalEventStore>();
        services.AddScoped<ISavingsRuleEventStore, MartenSavingsRuleEventStore>();
        services.AddScoped<IInvestmentEventStore, MartenInvestmentEventStore>();

        services.AddHttpClient<INotificationClient, NotificationHttpClient>(client =>
        {
            client.BaseAddress = new Uri(identityApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}
