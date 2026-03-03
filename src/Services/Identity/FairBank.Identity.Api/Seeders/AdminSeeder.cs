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
        var demoAccounts = new[]
        {
            (settings.Email, settings.Password, settings.FirstName, settings.LastName, UserRole.Admin),
            ("client@fairbank.cz", "Client123!", "Jan", "Novák", UserRole.Client),
            ("banker@fairbank.cz", "Banker123!", "Marie", "Svobodová", UserRole.Banker),
        };

        var anyAdded = false;

        foreach (var (emailStr, password, firstName, lastName, role) in demoAccounts)
        {
            var email = Email.Create(emailStr);

            if (await userRepository.ExistsWithEmailAsync(email))
                continue;

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

            var user = User.Create(firstName, lastName, email, passwordHash, role);

            await userRepository.AddAsync(user);
            anyAdded = true;
        }

        if (anyAdded)
            await unitOfWork.SaveChangesAsync();
    }
}
