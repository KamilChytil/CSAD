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
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var settings = scope.ServiceProvider
            .GetRequiredService<IOptions<AdminSeederSettings>>().Value;
        var userRepository = scope.ServiceProvider
            .GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();

        var email = Email.Create(settings.Email);

        if (await userRepository.ExistsWithEmailAsync(email))
            return;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(settings.Password, workFactor: 12);

        var admin = User.Create(
            settings.FirstName,
            settings.LastName,
            email,
            passwordHash,
            UserRole.Admin);

        await userRepository.AddAsync(admin);
        await unitOfWork.SaveChangesAsync();
    }
}
