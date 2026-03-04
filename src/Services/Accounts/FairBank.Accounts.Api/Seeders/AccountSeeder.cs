using FairBank.Accounts.Application.Commands.CreateAccount;
using FairBank.Accounts.Application.Commands.DepositMoney;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using Marten;
using MediatR;

namespace FairBank.Accounts.Api.Seeders;

/// <summary>
/// Creates one CZK account per demo user on startup and deposits an initial balance.
/// Idempotent — skips users who already have an account.
/// The GUIDs here MUST match AdminSeeder in the Identity service.
/// </summary>
public static class AccountSeeder
{
    // These must stay in sync with Identity.Api.Seeders.AdminSeeder fixed GUIDs.
    private static readonly Guid AdminSeedId  = new("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid ClientSeedId = new("c1000000-0000-0000-0000-000000000002");
    private static readonly Guid BankerSeedId = new("b1000000-0000-0000-0000-000000000003");

    // Deterministic account numbers so they stay the same across restarts
    private static readonly (Guid OwnerId, string AccountNumber, decimal Amount, string Description)[] SeedAccounts =
    [
        (AdminSeedId,  "000000-1000000001/8888", 100_000m, "Počáteční vklad — Vedení VA-BANK"),
        (BankerSeedId, "000000-2000000002/8888",  50_000m, "Počáteční vklad — Bankéř"),
        (ClientSeedId, "000000-3000000003/8888",  10_000m, "Startovní bonus pro nového klienta"),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        try
        {
            using var scope = services.CreateScope();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var store  = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

            await using var session = store.LightweightSession();

            foreach (var (ownerId, accountNumber, amount, description) in SeedAccounts)
            {
                // Check via inline snapshot — more reliable than raw event queries
                var hasAccount = await session.Query<Account>()
                    .AnyAsync(a => a.OwnerId == ownerId);

                if (hasAccount)
                    continue;

                // Create the account with deterministic number
                var account = await sender.Send(new CreateAccountCommand(ownerId, Currency.CZK, accountNumber));

                // Deposit starting balance
                await sender.Send(new DepositMoneyCommand(
                    account.Id,
                    amount,
                    Currency.CZK,
                    description));
            }
        }
        catch (Exception ex)
        {
            // Log but don't crash the service — seeding is best-effort
            var logger = services.GetService<ILoggerFactory>()?.CreateLogger("AccountSeeder");
            logger?.LogWarning(ex, "Account seeding failed (will retry on next restart)");
        }
    }
}
