using FairBank.Chat.Domain.Aggregates;
using FairBank.Chat.Domain.Ports;
using FairBank.Chat.Infrastructure.Persistence;
using FairBank.Chat.Infrastructure.Persistence.Repositories;
using FairBank.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Chat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddChatInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ChatDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ChatDbContext>());

        return services;
    }
}
