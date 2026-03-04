# Exchange Smenarna Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Transform the existing `/kurzy` page into a full in-app currency exchange (smenarna) with real money transfers between user accounts, favorite pairs, and exchange history.

**Architecture:** Extend the existing Payments microservice with new CQRS commands/queries, domain entities, and EF Core configurations. Add an `ExchangeRateService` that fetches rates from `fawazahmed0/currency-api` with in-memory caching. Frontend rewrite of `Exchange.razor` to a full exchange form with 10-second rate auto-refresh.

**Tech Stack:** .NET 10, C# 14, EF Core + Npgsql, MediatR, Blazor WASM, IMemoryCache, YARP API Gateway

---

### Task 1: Domain Entities — ExchangeTransaction and ExchangeFavorite

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Domain/Entities/ExchangeTransaction.cs`
- Create: `src/Services/Payments/FairBank.Payments.Domain/Entities/ExchangeFavorite.cs`

**Step 1: Create ExchangeTransaction entity**

Follow the exact same pattern as `Payment.cs` — `AggregateRoot<Guid>`, private parameterless constructor for EF, static `Create()` factory.

```csharp
using FairBank.Payments.Domain.Enums;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Entities;

public sealed class ExchangeTransaction : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid SourceAccountId { get; private set; }
    public Guid TargetAccountId { get; private set; }
    public Currency FromCurrency { get; private set; }
    public Currency ToCurrency { get; private set; }
    public decimal SourceAmount { get; private set; }
    public decimal TargetAmount { get; private set; }
    public decimal ExchangeRate { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ExchangeTransaction() { } // EF Core

    public static ExchangeTransaction Create(
        Guid userId,
        Guid sourceAccountId,
        Guid targetAccountId,
        Currency fromCurrency,
        Currency toCurrency,
        decimal sourceAmount,
        decimal targetAmount,
        decimal exchangeRate)
    {
        if (sourceAmount <= 0) throw new ArgumentException("Source amount must be positive.", nameof(sourceAmount));
        if (targetAmount <= 0) throw new ArgumentException("Target amount must be positive.", nameof(targetAmount));
        if (exchangeRate <= 0) throw new ArgumentException("Exchange rate must be positive.", nameof(exchangeRate));
        if (fromCurrency == toCurrency) throw new ArgumentException("Cannot exchange same currency.");

        return new ExchangeTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SourceAccountId = sourceAccountId,
            TargetAccountId = targetAccountId,
            FromCurrency = fromCurrency,
            ToCurrency = toCurrency,
            SourceAmount = sourceAmount,
            TargetAmount = targetAmount,
            ExchangeRate = exchangeRate,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

**Step 2: Create ExchangeFavorite entity**

```csharp
using FairBank.Payments.Domain.Enums;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Entities;

public sealed class ExchangeFavorite : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Currency FromCurrency { get; private set; }
    public Currency ToCurrency { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ExchangeFavorite() { } // EF Core

    public static ExchangeFavorite Create(
        Guid userId,
        Currency fromCurrency,
        Currency toCurrency)
    {
        if (fromCurrency == toCurrency) throw new ArgumentException("Cannot favorite same currency pair.");

        return new ExchangeFavorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FromCurrency = fromCurrency,
            ToCurrency = toCurrency,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

**Step 3: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Domain/Entities/ExchangeTransaction.cs src/Services/Payments/FairBank.Payments.Domain/Entities/ExchangeFavorite.cs
git commit -m "feat(exchange): add ExchangeTransaction and ExchangeFavorite domain entities"
```

---

### Task 2: Repository Interfaces — Domain Layer

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Domain/Repositories/IExchangeTransactionRepository.cs`
- Create: `src/Services/Payments/FairBank.Payments.Domain/Repositories/IExchangeFavoriteRepository.cs`

**Step 1: Create IExchangeTransactionRepository**

Check existing repository interfaces for the exact pattern. They use `IRepository<TAggregate, TId>` from SharedKernel as base.

```csharp
using FairBank.Payments.Domain.Entities;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Repositories;

public interface IExchangeTransactionRepository : IRepository<ExchangeTransaction, Guid>
{
    Task<IReadOnlyList<ExchangeTransaction>> GetByUserIdAsync(Guid userId, int limit = 20, CancellationToken ct = default);
}
```

**Step 2: Create IExchangeFavoriteRepository**

```csharp
using FairBank.Payments.Domain.Entities;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Repositories;

public interface IExchangeFavoriteRepository : IRepository<ExchangeFavorite, Guid>
{
    Task<IReadOnlyList<ExchangeFavorite>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
```

**Step 3: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Domain/Repositories/IExchangeTransactionRepository.cs src/Services/Payments/FairBank.Payments.Domain/Repositories/IExchangeFavoriteRepository.cs
git commit -m "feat(exchange): add repository interfaces for exchange entities"
```

---

### Task 3: EF Core Configuration and DbContext Update

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/Configurations/ExchangeTransactionConfiguration.cs`
- Create: `src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/Configurations/ExchangeFavoriteConfiguration.cs`
- Modify: `src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/PaymentsDbContext.cs`

**Step 1: Create ExchangeTransactionConfiguration**

Follow the exact same pattern as `PaymentConfiguration.cs` — `IEntityTypeConfiguration<T>`, snake_case columns, `HasConversion<string>()` for enums.

```csharp
using FairBank.Payments.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Payments.Infrastructure.Persistence.Configurations;

public sealed class ExchangeTransactionConfiguration : IEntityTypeConfiguration<ExchangeTransaction>
{
    public void Configure(EntityTypeBuilder<ExchangeTransaction> builder)
    {
        builder.ToTable("exchange_transactions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.SourceAccountId).HasColumnName("source_account_id").IsRequired();
        builder.Property(e => e.TargetAccountId).HasColumnName("target_account_id").IsRequired();

        builder.Property(e => e.FromCurrency).HasColumnName("from_currency")
            .HasConversion<string>().HasMaxLength(3).IsRequired();
        builder.Property(e => e.ToCurrency).HasColumnName("to_currency")
            .HasConversion<string>().HasMaxLength(3).IsRequired();

        builder.Property(e => e.SourceAmount).HasColumnName("source_amount")
            .HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.TargetAmount).HasColumnName("target_amount")
            .HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.ExchangeRate).HasColumnName("exchange_rate")
            .HasColumnType("decimal(18,6)").IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.CreatedAt).IsDescending();
    }
}
```

**Step 2: Create ExchangeFavoriteConfiguration**

```csharp
using FairBank.Payments.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Payments.Infrastructure.Persistence.Configurations;

public sealed class ExchangeFavoriteConfiguration : IEntityTypeConfiguration<ExchangeFavorite>
{
    public void Configure(EntityTypeBuilder<ExchangeFavorite> builder)
    {
        builder.ToTable("exchange_favorites");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(e => e.FromCurrency).HasColumnName("from_currency")
            .HasConversion<string>().HasMaxLength(3).IsRequired();
        builder.Property(e => e.ToCurrency).HasColumnName("to_currency")
            .HasConversion<string>().HasMaxLength(3).IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.UserId);
    }
}
```

**Step 3: Add DbSets to PaymentsDbContext**

In `PaymentsDbContext.cs`, add two new `DbSet` properties alongside the existing ones:

```csharp
public DbSet<ExchangeTransaction> ExchangeTransactions => Set<ExchangeTransaction>();
public DbSet<ExchangeFavorite> ExchangeFavorites => Set<ExchangeFavorite>();
```

Add the corresponding `using FairBank.Payments.Domain.Entities;` if not already present.

**Step 4: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/Configurations/ExchangeTransactionConfiguration.cs src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/Configurations/ExchangeFavoriteConfiguration.cs src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/PaymentsDbContext.cs
git commit -m "feat(exchange): add EF Core configurations and DbSets for exchange entities"
```

---

### Task 4: Repository Implementations

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/Repositories/ExchangeTransactionRepository.cs`
- Create: `src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/Repositories/ExchangeFavoriteRepository.cs`

**Step 1: Create ExchangeTransactionRepository**

Follow the exact same pattern as `PaymentRepository.cs` — primary constructor with `PaymentsDbContext`.

```csharp
using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Repositories;
using FairBank.Payments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Payments.Infrastructure.Persistence.Repositories;

public sealed class ExchangeTransactionRepository(PaymentsDbContext context)
    : IExchangeTransactionRepository
{
    public async Task<ExchangeTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.ExchangeTransactions.FindAsync([id], ct);

    public async Task AddAsync(ExchangeTransaction entity, CancellationToken ct = default)
        => await context.ExchangeTransactions.AddAsync(entity, ct);

    public void Update(ExchangeTransaction entity)
        => context.ExchangeTransactions.Update(entity);

    public async Task<IReadOnlyList<ExchangeTransaction>> GetByUserIdAsync(
        Guid userId, int limit = 20, CancellationToken ct = default)
        => await context.ExchangeTransactions
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
}
```

**Step 2: Create ExchangeFavoriteRepository**

```csharp
using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Repositories;
using FairBank.Payments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Payments.Infrastructure.Persistence.Repositories;

public sealed class ExchangeFavoriteRepository(PaymentsDbContext context)
    : IExchangeFavoriteRepository
{
    public async Task<ExchangeFavorite?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.ExchangeFavorites.FindAsync([id], ct);

    public async Task AddAsync(ExchangeFavorite entity, CancellationToken ct = default)
        => await context.ExchangeFavorites.AddAsync(entity, ct);

    public void Update(ExchangeFavorite entity)
        => context.ExchangeFavorites.Update(entity);

    public async Task<IReadOnlyList<ExchangeFavorite>> GetByUserIdAsync(
        Guid userId, CancellationToken ct = default)
        => await context.ExchangeFavorites
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
}
```

**Step 3: Register repositories in DI**

In `src/Services/Payments/FairBank.Payments.Infrastructure/DependencyInjection.cs`, add inside `AddPaymentsInfrastructure()`:

```csharp
services.AddScoped<IExchangeTransactionRepository, ExchangeTransactionRepository>();
services.AddScoped<IExchangeFavoriteRepository, ExchangeFavoriteRepository>();
```

Add the necessary `using` statements for the repository interfaces and implementations.

**Step 4: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/Repositories/ExchangeTransactionRepository.cs src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/Repositories/ExchangeFavoriteRepository.cs src/Services/Payments/FairBank.Payments.Infrastructure/DependencyInjection.cs
git commit -m "feat(exchange): add repository implementations and DI registration"
```

---

### Task 5: ExchangeRateService — Backend Rate Caching

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Services/IExchangeRateService.cs`
- Create: `src/Services/Payments/FairBank.Payments.Infrastructure/Services/ExchangeRateService.cs`
- Modify: `src/Services/Payments/FairBank.Payments.Infrastructure/DependencyInjection.cs`

**Step 1: Create IExchangeRateService interface in Application layer**

```csharp
namespace FairBank.Payments.Application.Exchange.Services;

public interface IExchangeRateService
{
    /// <summary>
    /// Get exchange rate between two currencies. Rate is relative to CZK as pivot.
    /// Returns the amount of 'to' currency you get for 1 unit of 'from' currency.
    /// </summary>
    Task<ExchangeRateResult?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken ct = default);
}

public sealed record ExchangeRateResult(
    decimal Rate,
    string FromCurrency,
    string ToCurrency,
    string RateDate);
```

**Step 2: Create ExchangeRateService implementation in Infrastructure**

This service fetches the full JSON from `fawazahmed0/currency-api`, caches it in `IMemoryCache` with 60s TTL, and computes pairwise rates via CZK as pivot.

```csharp
using System.Text.Json;
using FairBank.Payments.Application.Exchange.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FairBank.Payments.Infrastructure.Services;

public sealed class ExchangeRateService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<ExchangeRateService> logger) : IExchangeRateService
{
    private const string CacheKey = "exchange_rates_czk";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private const string PrimaryUrl =
        "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/czk.json";
    private const string FallbackUrl =
        "https://latest.currency-api.pages.dev/v1/currencies/czk.json";

    public async Task<ExchangeRateResult?> GetRateAsync(
        string fromCurrency, string toCurrency, CancellationToken ct = default)
    {
        var data = await GetCachedRatesAsync(ct);
        if (data is null) return null;

        var from = fromCurrency.ToLowerInvariant();
        var to = toCurrency.ToLowerInvariant();

        // Rates are relative to CZK: rates["eur"] = 0.041 means 1 CZK = 0.041 EUR
        // To get FROM->TO: (1/rates[from]) * rates[to]
        // Special case: if from is "czk", rate = rates[to]
        // Special case: if to is "czk", rate = 1/rates[from]

        decimal rate;
        if (from == "czk" && data.Rates.TryGetValue(to, out var toRate))
        {
            rate = toRate;
        }
        else if (to == "czk" && data.Rates.TryGetValue(from, out var fromRate) && fromRate != 0)
        {
            rate = 1m / fromRate;
        }
        else if (data.Rates.TryGetValue(from, out var fRate) && fRate != 0
              && data.Rates.TryGetValue(to, out var tRate))
        {
            rate = tRate / fRate;
        }
        else
        {
            return null;
        }

        return new ExchangeRateResult(rate, fromCurrency.ToUpperInvariant(), toCurrency.ToUpperInvariant(), data.Date);
    }

    private async Task<CachedRateData?> GetCachedRatesAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out CachedRateData? cached))
            return cached;

        var json = await FetchRatesJsonAsync(ct);
        if (json is null) return null;

        var parsed = ParseRatesJson(json);
        if (parsed is null) return null;

        cache.Set(CacheKey, parsed, CacheTtl);
        return parsed;
    }

    private async Task<string?> FetchRatesJsonAsync(CancellationToken ct)
    {
        try
        {
            return await httpClient.GetStringAsync(PrimaryUrl, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Primary exchange rate URL failed, trying fallback");
        }

        try
        {
            return await httpClient.GetStringAsync(FallbackUrl, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Both exchange rate URLs failed");
            return null;
        }
    }

    private static CachedRateData? ParseRatesJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var date = root.GetProperty("date").GetString() ?? "";
            var czkElement = root.GetProperty("czk");

            var rates = new Dictionary<string, decimal>();
            foreach (var prop in czkElement.EnumerateObject())
            {
                if (prop.Value.TryGetDecimal(out var rate))
                    rates[prop.Name] = rate;
            }

            return new CachedRateData(date, rates);
        }
        catch
        {
            return null;
        }
    }

    private sealed record CachedRateData(string Date, Dictionary<string, decimal> Rates);
}
```

**Step 3: Register ExchangeRateService in DI**

In `DependencyInjection.cs`, add inside `AddPaymentsInfrastructure()`:

```csharp
services.AddMemoryCache();
services.AddHttpClient<IExchangeRateService, ExchangeRateService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
```

Add the necessary `using` for `FairBank.Payments.Application.Exchange.Services`.

**Step 4: Verify the project builds**

Run: `dotnet build src/Services/Payments/FairBank.Payments.Api/FairBank.Payments.Api.csproj`
Expected: Build succeeded

**Step 5: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Application/Exchange/Services/IExchangeRateService.cs src/Services/Payments/FairBank.Payments.Infrastructure/Services/ExchangeRateService.cs src/Services/Payments/FairBank.Payments.Infrastructure/DependencyInjection.cs
git commit -m "feat(exchange): add ExchangeRateService with IMemoryCache caching"
```

---

### Task 6: Application Layer — DTOs

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/DTOs/ExchangeRateResponse.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/DTOs/ExchangeTransactionResponse.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/DTOs/ExchangeFavoriteResponse.cs`

**Step 1: Create all DTOs**

Follow the exact same pattern as `PaymentResponse.cs` — `sealed record` types.

```csharp
// ExchangeRateResponse.cs
namespace FairBank.Payments.Application.Exchange.DTOs;

public sealed record ExchangeRateResponse(
    decimal Rate,
    string FromCurrency,
    string ToCurrency,
    string RateDate);
```

```csharp
// ExchangeTransactionResponse.cs
namespace FairBank.Payments.Application.Exchange.DTOs;

public sealed record ExchangeTransactionResponse(
    Guid Id,
    Guid SourceAccountId,
    Guid TargetAccountId,
    string FromCurrency,
    string ToCurrency,
    decimal SourceAmount,
    decimal TargetAmount,
    decimal ExchangeRate,
    DateTime CreatedAt);
```

```csharp
// ExchangeFavoriteResponse.cs
namespace FairBank.Payments.Application.Exchange.DTOs;

public sealed record ExchangeFavoriteResponse(
    Guid Id,
    string FromCurrency,
    string ToCurrency,
    DateTime CreatedAt);
```

**Step 2: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Application/Exchange/DTOs/
git commit -m "feat(exchange): add exchange DTO records"
```

---

### Task 7: Application Layer — GetExchangeRate Query

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Queries/GetExchangeRate/GetExchangeRateQuery.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Queries/GetExchangeRate/GetExchangeRateQueryHandler.cs`

**Step 1: Create Query and Handler**

```csharp
// GetExchangeRateQuery.cs
using FairBank.Payments.Application.Exchange.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetExchangeRate;

public sealed record GetExchangeRateQuery(
    string FromCurrency,
    string ToCurrency) : IRequest<ExchangeRateResponse?>;
```

```csharp
// GetExchangeRateQueryHandler.cs
using FairBank.Payments.Application.Exchange.DTOs;
using FairBank.Payments.Application.Exchange.Services;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetExchangeRate;

public sealed class GetExchangeRateQueryHandler(
    IExchangeRateService exchangeRateService)
    : IRequestHandler<GetExchangeRateQuery, ExchangeRateResponse?>
{
    public async Task<ExchangeRateResponse?> Handle(
        GetExchangeRateQuery request, CancellationToken cancellationToken)
    {
        var result = await exchangeRateService.GetRateAsync(
            request.FromCurrency, request.ToCurrency, cancellationToken);

        if (result is null) return null;

        return new ExchangeRateResponse(
            result.Rate, result.FromCurrency, result.ToCurrency, result.RateDate);
    }
}
```

**Step 2: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Application/Exchange/Queries/GetExchangeRate/
git commit -m "feat(exchange): add GetExchangeRate query and handler"
```

---

### Task 8: Application Layer — ExecuteExchange Command

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Commands/ExecuteExchange/ExecuteExchangeCommand.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Commands/ExecuteExchange/ExecuteExchangeCommandHandler.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Commands/ExecuteExchange/ExecuteExchangeCommandValidator.cs`

**Step 1: Create Command**

```csharp
// ExecuteExchangeCommand.cs
using FairBank.Payments.Application.Exchange.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.ExecuteExchange;

public sealed record ExecuteExchangeCommand(
    Guid UserId,
    Guid SourceAccountId,
    Guid TargetAccountId,
    decimal Amount,
    string FromCurrency,
    string ToCurrency) : IRequest<ExchangeTransactionResponse>;
```

**Step 2: Create Validator**

```csharp
// ExecuteExchangeCommandValidator.cs
using FluentValidation;

namespace FairBank.Payments.Application.Exchange.Commands.ExecuteExchange;

public sealed class ExecuteExchangeCommandValidator : AbstractValidator<ExecuteExchangeCommand>
{
    private static readonly HashSet<string> AllowedCurrencies = new(StringComparer.OrdinalIgnoreCase)
        { "CZK", "EUR", "USD", "GBP" };

    public ExecuteExchangeCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.SourceAccountId).NotEmpty();
        RuleFor(x => x.TargetAccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_000_000);
        RuleFor(x => x.FromCurrency).NotEmpty()
            .Must(c => AllowedCurrencies.Contains(c))
            .WithMessage("Only CZK, EUR, USD, GBP are supported for exchange.");
        RuleFor(x => x.ToCurrency).NotEmpty()
            .Must(c => AllowedCurrencies.Contains(c))
            .WithMessage("Only CZK, EUR, USD, GBP are supported for exchange.");
        RuleFor(x => x)
            .Must(x => !string.Equals(x.FromCurrency, x.ToCurrency, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Cannot exchange same currency.");
    }
}
```

**Step 3: Create Handler**

This is the core handler. It:
1. Gets the current rate from `IExchangeRateService`
2. Calculates target amount
3. Calls `IAccountsServiceClient` to withdraw from source account
4. Calls `IAccountsServiceClient` to deposit to target account
5. Persists an `ExchangeTransaction` record

```csharp
// ExecuteExchangeCommandHandler.cs
using FairBank.Payments.Application.Exchange.DTOs;
using FairBank.Payments.Application.Exchange.Services;
using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Enums;
using FairBank.Payments.Domain.Repositories;
using FairBank.Payments.Infrastructure.HttpClients;
using FairBank.SharedKernel.Domain;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.ExecuteExchange;

public sealed class ExecuteExchangeCommandHandler(
    IExchangeRateService exchangeRateService,
    IExchangeTransactionRepository transactionRepository,
    IAccountsServiceClient accountsClient,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ExecuteExchangeCommand, ExchangeTransactionResponse>
{
    public async Task<ExchangeTransactionResponse> Handle(
        ExecuteExchangeCommand request, CancellationToken cancellationToken)
    {
        // 1. Get current rate
        var rateResult = await exchangeRateService.GetRateAsync(
            request.FromCurrency, request.ToCurrency, cancellationToken);

        if (rateResult is null)
            throw new InvalidOperationException("Exchange rate unavailable. Please try again later.");

        // 2. Calculate target amount (round to 2 decimal places)
        var targetAmount = Math.Round(request.Amount * rateResult.Rate, 2);

        if (targetAmount <= 0)
            throw new InvalidOperationException("Calculated target amount is zero or negative.");

        // 3. Parse currencies
        var fromCurrency = Enum.Parse<Currency>(request.FromCurrency, ignoreCase: true);
        var toCurrency = Enum.Parse<Currency>(request.ToCurrency, ignoreCase: true);

        // 4. Withdraw from source account
        var withdrawDescription = $"Smena {request.FromCurrency.ToUpperInvariant()} -> {request.ToCurrency.ToUpperInvariant()}";
        try
        {
            await accountsClient.WithdrawAsync(
                request.SourceAccountId, request.Amount, request.FromCurrency, withdrawDescription);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Withdrawal failed: {ex.Message}", ex);
        }

        // 5. Deposit to target account
        var depositDescription = $"Smena {request.FromCurrency.ToUpperInvariant()} -> {request.ToCurrency.ToUpperInvariant()}";
        try
        {
            await accountsClient.DepositAsync(
                request.TargetAccountId, targetAmount, request.ToCurrency, depositDescription);
        }
        catch (Exception ex)
        {
            // Compensate: deposit back to source account
            try
            {
                await accountsClient.DepositAsync(
                    request.SourceAccountId, request.Amount, request.FromCurrency,
                    "Kompenzace - neuspesna smena");
            }
            catch
            {
                // Log critical: compensation failed
            }

            throw new InvalidOperationException($"Deposit failed, funds returned: {ex.Message}", ex);
        }

        // 6. Persist exchange transaction
        var transaction = ExchangeTransaction.Create(
            request.UserId,
            request.SourceAccountId,
            request.TargetAccountId,
            fromCurrency,
            toCurrency,
            request.Amount,
            targetAmount,
            rateResult.Rate);

        await transactionRepository.AddAsync(transaction, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ExchangeTransactionResponse(
            transaction.Id,
            transaction.SourceAccountId,
            transaction.TargetAccountId,
            transaction.FromCurrency.ToString(),
            transaction.ToCurrency.ToString(),
            transaction.SourceAmount,
            transaction.TargetAmount,
            transaction.ExchangeRate,
            transaction.CreatedAt);
    }
}
```

**Important:** Check the exact namespace/interface for `IAccountsServiceClient`. It lives in `FairBank.Payments.Infrastructure.HttpClients` — but the Application layer should not reference Infrastructure directly. Check if there's an abstraction in the Application or Domain layer. If the existing `SendPaymentCommandHandler` references it directly from Infrastructure, follow the same pattern.

**Step 4: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Application/Exchange/Commands/ExecuteExchange/
git commit -m "feat(exchange): add ExecuteExchange command with withdraw/deposit and compensation"
```

---

### Task 9: Application Layer — GetExchangeHistory Query

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Queries/GetExchangeHistory/GetExchangeHistoryQuery.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Queries/GetExchangeHistory/GetExchangeHistoryQueryHandler.cs`

**Step 1: Create Query and Handler**

```csharp
// GetExchangeHistoryQuery.cs
using FairBank.Payments.Application.Exchange.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetExchangeHistory;

public sealed record GetExchangeHistoryQuery(
    Guid UserId,
    int Limit = 20) : IRequest<IReadOnlyList<ExchangeTransactionResponse>>;
```

```csharp
// GetExchangeHistoryQueryHandler.cs
using FairBank.Payments.Application.Exchange.DTOs;
using FairBank.Payments.Domain.Repositories;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetExchangeHistory;

public sealed class GetExchangeHistoryQueryHandler(
    IExchangeTransactionRepository repository)
    : IRequestHandler<GetExchangeHistoryQuery, IReadOnlyList<ExchangeTransactionResponse>>
{
    public async Task<IReadOnlyList<ExchangeTransactionResponse>> Handle(
        GetExchangeHistoryQuery request, CancellationToken cancellationToken)
    {
        var transactions = await repository.GetByUserIdAsync(
            request.UserId, request.Limit, cancellationToken);

        return transactions.Select(t => new ExchangeTransactionResponse(
            t.Id,
            t.SourceAccountId,
            t.TargetAccountId,
            t.FromCurrency.ToString(),
            t.ToCurrency.ToString(),
            t.SourceAmount,
            t.TargetAmount,
            t.ExchangeRate,
            t.CreatedAt)).ToList();
    }
}
```

**Step 2: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Application/Exchange/Queries/GetExchangeHistory/
git commit -m "feat(exchange): add GetExchangeHistory query"
```

---

### Task 10: Application Layer — Favorites (Add, Remove, Get)

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Commands/AddFavorite/AddFavoriteCommand.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Commands/AddFavorite/AddFavoriteCommandHandler.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Commands/RemoveFavorite/RemoveFavoriteCommand.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Commands/RemoveFavorite/RemoveFavoriteCommandHandler.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Queries/GetFavorites/GetFavoritesQuery.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Exchange/Queries/GetFavorites/GetFavoritesQueryHandler.cs`

**Step 1: AddFavoriteCommand**

```csharp
// AddFavoriteCommand.cs
using FairBank.Payments.Application.Exchange.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.AddFavorite;

public sealed record AddFavoriteCommand(
    Guid UserId,
    string FromCurrency,
    string ToCurrency) : IRequest<ExchangeFavoriteResponse>;
```

```csharp
// AddFavoriteCommandHandler.cs
using FairBank.Payments.Application.Exchange.DTOs;
using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Enums;
using FairBank.Payments.Domain.Repositories;
using FairBank.SharedKernel.Domain;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.AddFavorite;

public sealed class AddFavoriteCommandHandler(
    IExchangeFavoriteRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AddFavoriteCommand, ExchangeFavoriteResponse>
{
    public async Task<ExchangeFavoriteResponse> Handle(
        AddFavoriteCommand request, CancellationToken cancellationToken)
    {
        var fromCurrency = Enum.Parse<Currency>(request.FromCurrency, ignoreCase: true);
        var toCurrency = Enum.Parse<Currency>(request.ToCurrency, ignoreCase: true);

        var favorite = ExchangeFavorite.Create(request.UserId, fromCurrency, toCurrency);

        await repository.AddAsync(favorite, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ExchangeFavoriteResponse(
            favorite.Id,
            favorite.FromCurrency.ToString(),
            favorite.ToCurrency.ToString(),
            favorite.CreatedAt);
    }
}
```

**Step 2: RemoveFavoriteCommand**

```csharp
// RemoveFavoriteCommand.cs
using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.RemoveFavorite;

public sealed record RemoveFavoriteCommand(Guid FavoriteId) : IRequest<bool>;
```

```csharp
// RemoveFavoriteCommandHandler.cs
using FairBank.Payments.Domain.Repositories;
using FairBank.SharedKernel.Domain;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Commands.RemoveFavorite;

public sealed class RemoveFavoriteCommandHandler(
    IExchangeFavoriteRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RemoveFavoriteCommand, bool>
{
    public async Task<bool> Handle(
        RemoveFavoriteCommand request, CancellationToken cancellationToken)
    {
        var favorite = await repository.GetByIdAsync(request.FavoriteId, cancellationToken);
        if (favorite is null) return false;

        // EF Core: remove by setting entity state to Deleted
        // Since there's no Remove method on the base interface,
        // check how existing code handles deletion. PaymentTemplate uses SoftDelete,
        // but for favorites we want hard delete.
        // Use DbContext directly or add a Remove method to the repository.
        throw new NotImplementedException("Check repository pattern for hard delete");
    }
}
```

**Important note:** The existing `IRepository<T, TId>` base interface only has `GetByIdAsync`, `AddAsync`, `Update`. There is no `Remove`/`Delete` method. The `DeleteTemplateCommand` uses soft delete (`template.SoftDelete()`). For exchange favorites, we need hard delete. Options:
1. Add a `Remove(T entity)` method to `IExchangeFavoriteRepository` interface
2. Inject `PaymentsDbContext` directly into the handler

Check how the `CancelStandingOrderCommand` handles deletion — it likely uses soft delete via `order.Cancel()` too. For favorites, add `void Remove(ExchangeFavorite entity)` to the `IExchangeFavoriteRepository` interface and implement it as `context.ExchangeFavorites.Remove(entity)` in the repository.

Update the repository interface:
```csharp
public interface IExchangeFavoriteRepository : IRepository<ExchangeFavorite, Guid>
{
    Task<IReadOnlyList<ExchangeFavorite>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    void Remove(ExchangeFavorite entity);
}
```

Update the repository implementation:
```csharp
public void Remove(ExchangeFavorite entity)
    => context.ExchangeFavorites.Remove(entity);
```

Then the handler:
```csharp
public async Task<bool> Handle(
    RemoveFavoriteCommand request, CancellationToken cancellationToken)
{
    var favorite = await repository.GetByIdAsync(request.FavoriteId, cancellationToken);
    if (favorite is null) return false;

    repository.Remove(favorite);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return true;
}
```

**Step 3: GetFavoritesQuery**

```csharp
// GetFavoritesQuery.cs
using FairBank.Payments.Application.Exchange.DTOs;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetFavorites;

public sealed record GetFavoritesQuery(Guid UserId) : IRequest<IReadOnlyList<ExchangeFavoriteResponse>>;
```

```csharp
// GetFavoritesQueryHandler.cs
using FairBank.Payments.Application.Exchange.DTOs;
using FairBank.Payments.Domain.Repositories;
using MediatR;

namespace FairBank.Payments.Application.Exchange.Queries.GetFavorites;

public sealed class GetFavoritesQueryHandler(
    IExchangeFavoriteRepository repository)
    : IRequestHandler<GetFavoritesQuery, IReadOnlyList<ExchangeFavoriteResponse>>
{
    public async Task<IReadOnlyList<ExchangeFavoriteResponse>> Handle(
        GetFavoritesQuery request, CancellationToken cancellationToken)
    {
        var favorites = await repository.GetByUserIdAsync(request.UserId, cancellationToken);

        return favorites.Select(f => new ExchangeFavoriteResponse(
            f.Id,
            f.FromCurrency.ToString(),
            f.ToCurrency.ToString(),
            f.CreatedAt)).ToList();
    }
}
```

**Step 4: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Application/Exchange/Commands/ src/Services/Payments/FairBank.Payments.Application/Exchange/Queries/GetFavorites/ src/Services/Payments/FairBank.Payments.Domain/Repositories/IExchangeFavoriteRepository.cs src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/Repositories/ExchangeFavoriteRepository.cs
git commit -m "feat(exchange): add favorites commands/queries (add, remove, get)"
```

---

### Task 11: API Endpoints — Minimal API

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Api/Endpoints/ExchangeEndpoints.cs`
- Modify: `src/Services/Payments/FairBank.Payments.Api/Program.cs`

**Step 1: Create ExchangeEndpoints**

Follow the exact same pattern as `PaymentEndpoints.cs` — static extension method, `MapGroup`, `ISender`.

```csharp
using FairBank.Payments.Application.Exchange.Commands.AddFavorite;
using FairBank.Payments.Application.Exchange.Commands.ExecuteExchange;
using FairBank.Payments.Application.Exchange.Commands.RemoveFavorite;
using FairBank.Payments.Application.Exchange.Queries.GetExchangeHistory;
using FairBank.Payments.Application.Exchange.Queries.GetExchangeRate;
using FairBank.Payments.Application.Exchange.Queries.GetFavorites;
using MediatR;

namespace FairBank.Payments.Api.Endpoints;

public static class ExchangeEndpoints
{
    public static void MapExchangeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/exchange").WithTags("Exchange");

        // GET /api/v1/exchange/rate?from=CZK&to=EUR
        group.MapGet("/rate", async (string from, string to, ISender sender) =>
        {
            var result = await sender.Send(new GetExchangeRateQuery(from, to));
            return result is null ? Results.NotFound("Rate not available") : Results.Ok(result);
        })
        .WithName("GetExchangeRate");

        // POST /api/v1/exchange/convert
        group.MapPost("/convert", async (ExecuteExchangeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/exchange/history/{result.Id}", result);
        })
        .WithName("ExecuteExchange")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        // GET /api/v1/exchange/history?userId={userId}&limit=20
        group.MapGet("/history", async (Guid userId, int? limit, ISender sender) =>
        {
            var result = await sender.Send(new GetExchangeHistoryQuery(userId, limit ?? 20));
            return Results.Ok(result);
        })
        .WithName("GetExchangeHistory");

        // GET /api/v1/exchange/favorites?userId={userId}
        group.MapGet("/favorites", async (Guid userId, ISender sender) =>
        {
            var result = await sender.Send(new GetFavoritesQuery(userId));
            return Results.Ok(result);
        })
        .WithName("GetExchangeFavorites");

        // POST /api/v1/exchange/favorites
        group.MapPost("/favorites", async (AddFavoriteCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/exchange/favorites/{result.Id}", result);
        })
        .WithName("AddExchangeFavorite")
        .Produces(StatusCodes.Status201Created);

        // DELETE /api/v1/exchange/favorites/{id}
        group.MapDelete("/favorites/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new RemoveFavoriteCommand(id));
            return result ? Results.NoContent() : Results.NotFound();
        })
        .WithName("RemoveExchangeFavorite");
    }
}
```

**Step 2: Register in Program.cs**

In `Program.cs`, add `app.MapExchangeEndpoints();` alongside the existing `app.MapPaymentEndpoints()` etc.

**Step 3: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Api/Endpoints/ExchangeEndpoints.cs src/Services/Payments/FairBank.Payments.Api/Program.cs
git commit -m "feat(exchange): add exchange API endpoints (rate, convert, history, favorites)"
```

---

### Task 12: API Gateway Route

**Files:**
- Modify: `src/FairBank.ApiGateway/appsettings.json`

**Step 1: Add exchange route**

In the `ReverseProxy.Routes` section, add a new route (alongside existing `standing-orders-route`, `payment-templates-route` which already use `payments-cluster`):

```json
"exchange-route": {
    "ClusterId": "payments-cluster",
    "Match": {
        "Path": "/api/v1/exchange/{**catch-all}"
    }
}
```

No new cluster needed — reuses the existing `payments-cluster` pointing to `http://payments-api:8080`.

**Step 2: Verify no route conflicts**

Existing routes use paths like `/api/v1/payments/{**catch-all}`, `/api/v1/standing-orders/{**catch-all}` etc. The new `/api/v1/exchange/{**catch-all}` does not conflict with any.

**Step 3: Commit**

```bash
git add src/FairBank.ApiGateway/appsettings.json
git commit -m "feat(exchange): add API gateway route for exchange endpoints"
```

---

### Task 13: Frontend API Client — Add Exchange Methods

**Files:**
- Modify: `src/FairBank.Web.Shared/Services/IFairBankApi.cs`
- Modify: `src/FairBank.Web.Shared/Services/FairBankApiClient.cs`
- Create: `src/FairBank.Web.Shared/Models/ExchangeModels.cs`

**Step 1: Create frontend DTOs**

```csharp
// ExchangeModels.cs
namespace FairBank.Web.Shared.Models;

public sealed record ExchangeRateDto(
    decimal Rate,
    string FromCurrency,
    string ToCurrency,
    string RateDate);

public sealed record ExchangeTransactionDto(
    Guid Id,
    Guid SourceAccountId,
    Guid TargetAccountId,
    string FromCurrency,
    string ToCurrency,
    decimal SourceAmount,
    decimal TargetAmount,
    decimal ExchangeRate,
    DateTime CreatedAt);

public sealed record ExchangeFavoriteDto(
    Guid Id,
    string FromCurrency,
    string ToCurrency,
    DateTime CreatedAt);

public sealed record ExecuteExchangeRequest(
    Guid UserId,
    Guid SourceAccountId,
    Guid TargetAccountId,
    decimal Amount,
    string FromCurrency,
    string ToCurrency);

public sealed record AddFavoriteRequest(
    Guid UserId,
    string FromCurrency,
    string ToCurrency);
```

**Step 2: Add methods to IFairBankApi**

Add these methods to the interface:

```csharp
// Exchange
Task<ExchangeRateDto?> GetExchangeRateAsync(string fromCurrency, string toCurrency);
Task<ExchangeTransactionDto> ExecuteExchangeAsync(ExecuteExchangeRequest request);
Task<List<ExchangeTransactionDto>> GetExchangeHistoryAsync(Guid userId, int limit = 20);
Task<List<ExchangeFavoriteDto>> GetExchangeFavoritesAsync(Guid userId);
Task<ExchangeFavoriteDto> AddExchangeFavoriteAsync(AddFavoriteRequest request);
Task RemoveExchangeFavoriteAsync(Guid favoriteId);
```

**Step 3: Implement in FairBankApiClient**

Add implementations following the exact same HTTP call pattern used throughout the class:

```csharp
// Exchange
public async Task<ExchangeRateDto?> GetExchangeRateAsync(string fromCurrency, string toCurrency)
{
    try
    {
        return await http.GetFromJsonAsync<ExchangeRateDto>(
            $"api/v1/exchange/rate?from={Uri.EscapeDataString(fromCurrency)}&to={Uri.EscapeDataString(toCurrency)}");
    }
    catch { return null; }
}

public async Task<ExchangeTransactionDto> ExecuteExchangeAsync(ExecuteExchangeRequest request)
{
    var response = await http.PostAsJsonAsync("api/v1/exchange/convert", request);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<ExchangeTransactionDto>())!;
}

public async Task<List<ExchangeTransactionDto>> GetExchangeHistoryAsync(Guid userId, int limit = 20)
{
    try
    {
        return await http.GetFromJsonAsync<List<ExchangeTransactionDto>>(
            $"api/v1/exchange/history?userId={userId}&limit={limit}") ?? [];
    }
    catch { return []; }
}

public async Task<List<ExchangeFavoriteDto>> GetExchangeFavoritesAsync(Guid userId)
{
    try
    {
        return await http.GetFromJsonAsync<List<ExchangeFavoriteDto>>(
            $"api/v1/exchange/favorites?userId={userId}") ?? [];
    }
    catch { return []; }
}

public async Task<ExchangeFavoriteDto> AddExchangeFavoriteAsync(AddFavoriteRequest request)
{
    var response = await http.PostAsJsonAsync("api/v1/exchange/favorites", request);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<ExchangeFavoriteDto>())!;
}

public async Task RemoveExchangeFavoriteAsync(Guid favoriteId)
{
    var response = await http.DeleteAsync($"api/v1/exchange/favorites/{favoriteId}");
    response.EnsureSuccessStatusCode();
}
```

**Step 4: Commit**

```bash
git add src/FairBank.Web.Shared/Models/ExchangeModels.cs src/FairBank.Web.Shared/Services/IFairBankApi.cs src/FairBank.Web.Shared/Services/FairBankApiClient.cs
git commit -m "feat(exchange): add exchange methods to frontend API client"
```

---

### Task 14: Frontend — Rewrite Exchange.razor

**Files:**
- Modify: `src/FairBank.Web.Exchange/Pages/Exchange.razor`

This is the largest task. Rewrite the entire page to the smenarna design.

**Step 1: Rewrite Exchange.razor**

Replace the entire file content. The page now:
- Injects `IFairBankApi Api` and `IAuthService Auth` (for authenticated backend calls)
- Still injects `HttpClient Http` (for fetching the full currency list from external API on initial load — needed for the dropdown of ~50 currencies and informational rate display)
- Has a `System.Threading.Timer` for 10-second auto-refresh of the rate
- Implements `IDisposable` to clean up the timer

Key structure:

```razor
@page "/kurzy"
@namespace FairBank.Web.Exchange.Pages
@using FairBank.Web.Exchange.Services
@using FairBank.Web.Shared.Models
@inject HttpClient Http
@inject IFairBankApi Api
@inject IAuthService Auth
@implements IDisposable

<PageHeader Title="SMENARNA" />

<div class="page-content">

    @* ── Exchange Form ── *@
    <ContentCard Title="Smena men">
        <ChildContent>
            <div class="exchange-form">
                <div class="form-group">
                    <label class="form-label">Z meny</label>
                    <select class="form-input" @bind="_fromCurrency" @bind:after="OnCurrencyChanged">
                        @foreach (var curr in _dropdownCurrencies)
                        {
                            <option value="@curr">@(CurrencyConverter.GetFlag(curr)) @(curr.ToUpperInvariant()) - @(CurrencyConverter.GetName(curr))</option>
                        }
                    </select>
                </div>

                <div class="exchange-swap">
                    <VbButton Variant="ghost" OnClick="SwapCurrencies">Prohodit</VbButton>
                </div>

                <div class="form-group">
                    <label class="form-label">Na menu</label>
                    <select class="form-input" @bind="_toCurrency" @bind:after="OnCurrencyChanged">
                        @foreach (var curr in _dropdownCurrencies)
                        {
                            <option value="@curr">@(CurrencyConverter.GetFlag(curr)) @(curr.ToUpperInvariant()) - @(CurrencyConverter.GetName(curr))</option>
                        }
                    </select>
                </div>

                <div class="form-group">
                    <label class="form-label">Castka</label>
                    <input class="form-input" type="number" min="0" step="0.01"
                           @bind="_amount" @bind:after="RecalculateTarget" />
                </div>

                @if (_currentRate is not null)
                {
                    <div class="rate-info-box">
                        <div class="rate-line">
                            <span>Kurz: 1 @(_fromCurrency.ToUpperInvariant()) = @(_currentRate.Rate.ToString("N4")) @(_toCurrency.ToUpperInvariant())</span>
                        </div>
                        <div class="rate-line rate-target">
                            <span>Obdrzite: @(_targetAmount.ToString("N2")) @(_toCurrency.ToUpperInvariant())</span>
                        </div>
                        <div class="rate-line rate-meta">
                            <span>Kurz z @(_currentRate.RateDate) (aktualizace za @(_refreshCountdown)s)</span>
                        </div>
                    </div>
                }
                else if (_rateLoading)
                {
                    <div class="rate-info-box">
                        <span>Nacitam kurz...</span>
                    </div>
                }

                @if (_canExchange)
                {
                    <div class="form-actions">
                        <VbButton Variant="primary" OnClick="ShowConfirmDialog" Disabled="@(_amount <= 0 || _currentRate is null || _exchangeInProgress)">
                            Prevest
                        </VbButton>
                    </div>
                }
                else if (_currentRate is not null)
                {
                    <div class="exchange-info-note">
                        Smena je mozna pouze mezi CZK, EUR, USD a GBP.
                    </div>
                }
            </div>
        </ChildContent>
    </ContentCard>

    @* ── Confirmation Dialog ── *@
    @if (_showConfirmDialog)
    {
        <ContentCard Title="Potvrzeni smeny">
            <ChildContent>
                <div class="confirm-details">
                    <p>Odecteno: @(_amount.ToString("N2")) @(_fromCurrency.ToUpperInvariant())</p>
                    <p>Pripsano: @(_targetAmount.ToString("N2")) @(_toCurrency.ToUpperInvariant())</p>
                    <p>Kurz: @(_currentRate!.Rate.ToString("N4"))</p>
                </div>
                <div class="form-actions">
                    <VbButton Variant="primary" OnClick="ExecuteExchange" Disabled="@_exchangeInProgress">
                        @(_exchangeInProgress ? "Provadim..." : "Potvrdit")
                    </VbButton>
                    <VbButton Variant="ghost" OnClick="CancelConfirm">Zrusit</VbButton>
                </div>
            </ChildContent>
        </ContentCard>
    }

    @* ── Success/Error message ── *@
    @if (_successMessage is not null)
    {
        <ContentCard>
            <ChildContent>
                <div class="exchange-success">@_successMessage</div>
            </ChildContent>
        </ContentCard>
    }

    @if (_errorMessage is not null)
    {
        <ContentCard>
            <ChildContent>
                <div class="exchange-error">@_errorMessage</div>
            </ChildContent>
        </ContentCard>
    }

    @* ── Favorites ── *@
    <ContentCard Title="Oblibene smeny">
        <HeaderAction>
            <VbButton Variant="ghost" Size="sm" OnClick="AddCurrentAsFavorite" Disabled="@(!_canExchange)">
                + Pridat
            </VbButton>
        </HeaderAction>
        <ChildContent>
            @if (_favorites.Count == 0)
            {
                <div class="empty-state-card">
                    <p class="empty-state">Zadne oblibene smeny</p>
                </div>
            }
            else
            {
                <div class="favorites-chips">
                    @foreach (var fav in _favorites)
                    {
                        <div class="favorite-chip">
                            <VbButton Variant="outline" Size="sm" OnClick="() => ApplyFavorite(fav)">
                                @fav.FromCurrency > @fav.ToCurrency
                            </VbButton>
                            <button class="chip-remove" @onclick="() => RemoveFavorite(fav.Id)">x</button>
                        </div>
                    }
                </div>
            }
        </ChildContent>
    </ContentCard>

    @* ── Exchange History ── *@
    <ContentCard Title="Historie smen">
        <ChildContent>
            @if (_history.Count == 0)
            {
                <div class="empty-state-card">
                    <p class="empty-state">Zadna historie smen</p>
                </div>
            }
            else
            {
                <div class="exchange-history">
                    @foreach (var tx in _history)
                    {
                        <div class="history-item">
                            <span class="history-date">@tx.CreatedAt.ToString("d.M.")</span>
                            <span class="history-amounts">@tx.SourceAmount.ToString("N2") @tx.FromCurrency > @tx.TargetAmount.ToString("N2") @tx.ToCurrency</span>
                            <span class="history-rate">@tx.ExchangeRate.ToString("N4")</span>
                        </div>
                    }
                </div>
            }
        </ChildContent>
    </ContentCard>

</div>

@code {
    // ── State ──
    private readonly CurrencyConverter _converter = new();
    private List<string> _dropdownCurrencies = [];
    private string _fromCurrency = "czk";
    private string _toCurrency = "eur";
    private decimal _amount = 1000;
    private decimal _targetAmount;

    private ExchangeRateDto? _currentRate;
    private bool _rateLoading;
    private int _refreshCountdown = 10;
    private Timer? _refreshTimer;

    private bool _canExchange;
    private bool _showConfirmDialog;
    private bool _exchangeInProgress;
    private string? _successMessage;
    private string? _errorMessage;

    private List<ExchangeFavoriteDto> _favorites = [];
    private List<ExchangeTransactionDto> _history = [];

    // Accounts for exchange (user's accounts in CZK/EUR/USD/GBP)
    private List<AccountResponse> _accounts = [];

    private static readonly HashSet<string> ExchangeableCurrencies =
        new(StringComparer.OrdinalIgnoreCase) { "CZK", "EUR", "USD", "GBP" };

    // ── Lifecycle ──
    protected override async Task OnInitializedAsync()
    {
        // Load currency list from external API (for dropdown)
        await LoadCurrencyList();

        // Load user data (accounts, favorites, history) if authenticated
        if (Auth.CurrentSession is not null)
        {
            var userId = Auth.CurrentSession.UserId;
            var accountsTask = Api.GetAccountsByOwnerAsync(userId);
            var favoritesTask = Api.GetExchangeFavoritesAsync(userId);
            var historyTask = Api.GetExchangeHistoryAsync(userId);

            await Task.WhenAll(accountsTask, favoritesTask, historyTask);

            _accounts = accountsTask.Result ?? [];
            _favorites = favoritesTask.Result ?? [];
            _history = historyTask.Result ?? [];
        }
    }

    private async Task LoadCurrencyList()
    {
        // Fetch from external API to populate dropdown with ~50 currencies
        try
        {
            var json = await Http.GetStringAsync(
                "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/czk.json");
            _converter.LoadRates(json);
            _dropdownCurrencies = _converter.GetFiatRates().Keys.OrderBy(c => c).ToList();
            if (!_dropdownCurrencies.Contains("czk"))
                _dropdownCurrencies.Insert(0, "czk");
        }
        catch
        {
            try
            {
                var json = await Http.GetStringAsync(
                    "https://latest.currency-api.pages.dev/v1/currencies/czk.json");
                _converter.LoadRates(json);
                _dropdownCurrencies = _converter.GetFiatRates().Keys.OrderBy(c => c).ToList();
                if (!_dropdownCurrencies.Contains("czk"))
                    _dropdownCurrencies.Insert(0, "czk");
            }
            catch
            {
                // Fallback to just the 4 exchangeable currencies
                _dropdownCurrencies = ["czk", "eur", "usd", "gbp"];
            }
        }
    }

    // ── Currency selection ──
    private async Task OnCurrencyChanged()
    {
        _successMessage = null;
        _errorMessage = null;
        _showConfirmDialog = false;

        _canExchange = ExchangeableCurrencies.Contains(_fromCurrency)
                    && ExchangeableCurrencies.Contains(_toCurrency)
                    && !string.Equals(_fromCurrency, _toCurrency, StringComparison.OrdinalIgnoreCase);

        if (string.Equals(_fromCurrency, _toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            _currentRate = null;
            _targetAmount = _amount;
            StopTimer();
            return;
        }

        await FetchRate();
        StartTimer();
    }

    private async Task SwapCurrencies()
    {
        (_fromCurrency, _toCurrency) = (_toCurrency, _fromCurrency);
        await OnCurrencyChanged();
    }

    // ── Rate fetching ──
    private async Task FetchRate()
    {
        _rateLoading = true;
        StateHasChanged();

        // Use backend API for the rate (cached, validated)
        _currentRate = await Api.GetExchangeRateAsync(_fromCurrency, _toCurrency);

        if (_currentRate is null)
        {
            // Fallback: use frontend converter for informational display
            var localRate = _converter.Convert(1, _fromCurrency, _toCurrency);
            if (localRate.HasValue)
            {
                _currentRate = new ExchangeRateDto(
                    localRate.Value, _fromCurrency.ToUpperInvariant(),
                    _toCurrency.ToUpperInvariant(), _converter.RateDate ?? "");
            }
        }

        _rateLoading = false;
        RecalculateTarget();
        _refreshCountdown = 10;
        StateHasChanged();
    }

    private void RecalculateTarget()
    {
        if (_currentRate is not null)
            _targetAmount = Math.Round(_amount * _currentRate.Rate, 2);
    }

    // ── 10s auto-refresh timer ──
    private void StartTimer()
    {
        StopTimer();
        _refreshCountdown = 10;
        _refreshTimer = new Timer(async _ =>
        {
            _refreshCountdown--;
            if (_refreshCountdown <= 0)
            {
                await InvokeAsync(async () =>
                {
                    await FetchRate();
                    StateHasChanged();
                });
            }
            else
            {
                await InvokeAsync(StateHasChanged);
            }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void StopTimer()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    // ── Exchange execution ──
    private void ShowConfirmDialog()
    {
        _showConfirmDialog = true;
        _successMessage = null;
        _errorMessage = null;
    }

    private void CancelConfirm() => _showConfirmDialog = false;

    private async Task ExecuteExchange()
    {
        if (Auth.CurrentSession is null || _currentRate is null) return;

        _exchangeInProgress = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            // Find source and target accounts by currency
            var sourceAccount = _accounts.FirstOrDefault(a =>
                string.Equals(a.Currency, _fromCurrency, StringComparison.OrdinalIgnoreCase));
            var targetAccount = _accounts.FirstOrDefault(a =>
                string.Equals(a.Currency, _toCurrency, StringComparison.OrdinalIgnoreCase));

            if (sourceAccount is null || targetAccount is null)
            {
                _errorMessage = "Nemate ucty v obou menach. Zalozze si ucet v pozadovane mene.";
                _exchangeInProgress = false;
                _showConfirmDialog = false;
                return;
            }

            var request = new ExecuteExchangeRequest(
                Auth.CurrentSession.UserId,
                sourceAccount.Id,
                targetAccount.Id,
                _amount,
                _fromCurrency.ToUpperInvariant(),
                _toCurrency.ToUpperInvariant());

            var result = await Api.ExecuteExchangeAsync(request);

            _successMessage = $"Uspesne smeneno {result.SourceAmount:N2} {result.FromCurrency} za {result.TargetAmount:N2} {result.ToCurrency}";
            _showConfirmDialog = false;

            // Reload history and accounts
            _history = await Api.GetExchangeHistoryAsync(Auth.CurrentSession.UserId);
            _accounts = await Api.GetAccountsByOwnerAsync(Auth.CurrentSession.UserId) ?? [];
        }
        catch (Exception ex)
        {
            _errorMessage = $"Smena se nezdarila: {ex.Message}";
            _showConfirmDialog = false;
        }
        finally
        {
            _exchangeInProgress = false;
        }
    }

    // ── Favorites ──
    private async Task AddCurrentAsFavorite()
    {
        if (Auth.CurrentSession is null) return;

        try
        {
            var request = new AddFavoriteRequest(
                Auth.CurrentSession.UserId,
                _fromCurrency.ToUpperInvariant(),
                _toCurrency.ToUpperInvariant());
            await Api.AddExchangeFavoriteAsync(request);
            _favorites = await Api.GetExchangeFavoritesAsync(Auth.CurrentSession.UserId);
        }
        catch { }
    }

    private async Task RemoveFavorite(Guid favoriteId)
    {
        if (Auth.CurrentSession is null) return;

        try
        {
            await Api.RemoveExchangeFavoriteAsync(favoriteId);
            _favorites = await Api.GetExchangeFavoritesAsync(Auth.CurrentSession.UserId);
        }
        catch { }
    }

    private async Task ApplyFavorite(ExchangeFavoriteDto favorite)
    {
        _fromCurrency = favorite.FromCurrency.ToLowerInvariant();
        _toCurrency = favorite.ToCurrency.ToLowerInvariant();
        await OnCurrencyChanged();
    }

    // ── Cleanup ──
    public void Dispose() => StopTimer();
}
```

**Step 2: Add scoped CSS**

Create or update inline `<style>` in the razor file (or a companion `.razor.css` file) with styles matching the VA-BANK theme. Key styles needed:

- `.exchange-form` — flex column layout
- `.exchange-swap` — centered swap button
- `.rate-info-box` — bordered box for rate display, uses `var(--vb-card-bg)` and `var(--vb-radius-sm)`
- `.rate-target` — larger font, `var(--vb-gold)` color
- `.exchange-info-note` — muted note text
- `.favorites-chips` — flex wrap row of chip buttons
- `.favorite-chip` — inline-flex with remove button
- `.exchange-history` — list of history items
- `.history-item` — flex row (date, amounts, rate)
- `.exchange-success` — green text
- `.exchange-error` — red text (`var(--vb-red)`)
- `.confirm-details` — styled summary box

All CSS must use `var(--vb-*)` tokens from `vabank-theme.css`. Check existing pages like Payments.razor for the exact inline style patterns.

**Step 3: Verify the frontend builds**

Run: `dotnet build src/FairBank.Web/FairBank.Web.csproj`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add src/FairBank.Web.Exchange/Pages/Exchange.razor
git commit -m "feat(exchange): rewrite Exchange.razor to full smenarna with rate refresh, favorites, history"
```

---

### Task 15: Build Verification and Smoke Test

**Step 1: Build the entire solution**

Run: `dotnet build` from the CSAD root directory.
Expected: Build succeeded with 0 errors.

Fix any compilation errors. Common issues:
- Missing `using` statements
- Namespace mismatches
- Missing project references (e.g. Infrastructure referencing Application types)

**Step 2: Check the Payments API starts**

If Docker compose is available:
Run: `docker compose up payments-api --build`
Check the API starts and the new tables are created in `payments_service` schema.

**Step 3: Test the exchange rate endpoint**

```bash
curl http://localhost:8003/api/v1/exchange/rate?from=CZK&to=EUR
```
Expected: JSON response with `rate`, `fromCurrency`, `toCurrency`, `rateDate`.

**Step 4: Commit any fixes**

```bash
git add -A
git commit -m "fix(exchange): resolve build issues"
```

---

### Task 16: Review and Polish

**Step 1: Review the AccountResponse model**

Check `src/FairBank.Web.Shared/Models/AccountResponse.cs` to confirm it has an `Id` (Guid) and `Currency` (string) property, as used in `ExecuteExchange` flow. If the property names differ, update Exchange.razor accordingly.

**Step 2: Verify auth flow**

Confirm that `Auth.CurrentSession` is checked on page load and exchange is only available to authenticated users. Add `<AuthGuard>` wrapper if other pages use it.

**Step 3: Edge cases to handle**

- Same currency selected for from/to: disable exchange button, show only informational message
- Rate fetch failure: show error state with retry
- Insufficient balance: handled by backend (`WithdrawAsync` will fail), display error
- User has no account in target currency: frontend check before calling convert

**Step 4: Final commit**

```bash
git add -A
git commit -m "feat(exchange): complete smenarna feature - rate caching, real transfers, favorites, history"
```
