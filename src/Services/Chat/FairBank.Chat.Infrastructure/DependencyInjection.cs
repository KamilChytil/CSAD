using FairBank.Chat.Domain.Ports;
using FairBank.Chat.Infrastructure.Persistence;
using FairBank.Chat.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Chat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddChatInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ChatDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();

        return services;
    }
}
