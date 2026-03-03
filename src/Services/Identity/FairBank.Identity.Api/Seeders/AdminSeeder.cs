using FairBank.Identity.Api.Configuration;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using Microsoft.Extensions.Options;

namespace FairBank.Identity.Api.Seeders;

public static class AdminSeeder
{
    // Fixed deterministic GUIDs so the Accounts service seeder can reference
    // the same users across service restarts without cross-schema queries.
    public static readonly Guid AdminSeedId  = new("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid ClientSeedId = new("c1000000-0000-0000-0000-000000000002");
    public static readonly Guid BankerSeedId = new("b1000000-0000-0000-0000-000000000003");

    /// <summary>
    /// Seeds all demo accounts on startup. Skips any that already exist (idempotent).
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var settings = scope.ServiceProvider
            .GetRequiredService<IOptions<AdminSeederSettings>>().Value;
        var userRepository = scope.ServiceProvider
            .GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();

        // All demo accounts to seed — admin from config, rest hardcoded for hackathon
        // Fixed GUIDs ensure the Accounts seeder can reference the same user IDs.
        var demoAccounts = new[]
        {
            (settings.Email, settings.Password, settings.FirstName, settings.LastName, UserRole.Admin,  AdminSeedId),
            ("client@fairbank.cz", "Client123!", "Jan", "Novák",       UserRole.Client, ClientSeedId),
            ("banker@fairbank.cz", "Banker123!", "Marie", "Svobodová",  UserRole.Banker, BankerSeedId),
        };

        var anyAdded = false;

        foreach (var (emailStr, password, firstName, lastName, role, seedId) in demoAccounts)
        {
            var email = Email.Create(emailStr);

            if (await userRepository.ExistsWithEmailAsync(email))
                continue;

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

            var user = User.Create(firstName, lastName, email, passwordHash, role, seedId);

            await userRepository.AddAsync(user);
            anyAdded = true;
        }

        if (anyAdded)
            await unitOfWork.SaveChangesAsync();
    }
}
