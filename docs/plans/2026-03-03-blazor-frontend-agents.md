# Va-bank — Full-Stack Agent Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement your assigned slice.

**Goal:** 5 AI agentů paralelně vyvíjí celý projekt Va-bank (casino-themed banking app). Každý agent vlastní **full-stack vertikální slice** — backend službu (Domain → Application → Infrastructure → API), frontend modul (Blazor WASM Razor Class Library) a unit testy. Sdílený kód je v `FairBank.SharedKernel` (backend) a `FairBank.Web.Shared` (frontend).

**Tech Stack:** .NET 10, C#, PostgreSQL 16, Marten (Event Sourcing), EF Core, MediatR, FluentValidation, YARP, Blazor WebAssembly, Docker Compose, xunit, FluentAssertions, NSubstitute

---

## Celková architektura

```
FairBank/
├── src/
│   ├── FairBank.SharedKernel/                     ← Sdílený backend (Entity, AggregateRoot, ValueObject, IRepository, IUnitOfWork)
│   ├── FairBank.ApiGateway/                       ← YARP reverse proxy (routuje /api/v1/* na interní služby)
│   │
│   ├── Services/
│   │   ├── Identity/                              ← ★ AGENT 5
│   │   │   ├── FairBank.Identity.Domain/
│   │   │   ├── FairBank.Identity.Application/
│   │   │   ├── FairBank.Identity.Infrastructure/
│   │   │   └── FairBank.Identity.Api/
│   │   ├── Accounts/                              ← ★ AGENT 1
│   │   │   ├── FairBank.Accounts.Domain/
│   │   │   ├── FairBank.Accounts.Application/
│   │   │   ├── FairBank.Accounts.Infrastructure/
│   │   │   └── FairBank.Accounts.Api/
│   │   ├── Payments/                              ← ★ AGENT 2 (NOVÝ — vytvořit)
│   │   │   ├── FairBank.Payments.Domain/
│   │   │   ├── FairBank.Payments.Application/
│   │   │   ├── FairBank.Payments.Infrastructure/
│   │   │   └── FairBank.Payments.Api/
│   │   ├── Savings/                               ← ★ AGENT 3 (NOVÝ — vytvořit)
│   │   │   ├── FairBank.Savings.Domain/
│   │   │   ├── FairBank.Savings.Application/
│   │   │   ├── FairBank.Savings.Infrastructure/
│   │   │   └── FairBank.Savings.Api/
│   │   └── Investments/                           ← ★ AGENT 4 (NOVÝ — vytvořit)
│   │       ├── FairBank.Investments.Domain/
│   │       ├── FairBank.Investments.Application/
│   │       ├── FairBank.Investments.Infrastructure/
│   │       └── FairBank.Investments.Api/
│   │
│   ├── FairBank.Web/                              ← App shell (routing, layout) — NEUPRAVOVAT
│   ├── FairBank.Web.Shared/                       ← Sdílené frontend komponenty, modely, API klient, CSS téma
│   ├── FairBank.Web.Overview/                     ← ★ AGENT 1 frontend
│   ├── FairBank.Web.Payments/                     ← ★ AGENT 2 frontend
│   ├── FairBank.Web.Savings/                      ← ★ AGENT 3 frontend
│   ├── FairBank.Web.Investments/                  ← ★ AGENT 4 frontend
│   └── FairBank.Web.Profile/                      ← ★ AGENT 5 frontend
│
├── tests/
│   ├── FairBank.Accounts.UnitTests/               ← ★ AGENT 1 testy
│   ├── FairBank.Payments.UnitTests/               ← ★ AGENT 2 testy (NOVÝ)
│   ├── FairBank.Savings.UnitTests/                ← ★ AGENT 3 testy (NOVÝ)
│   ├── FairBank.Investments.UnitTests/            ← ★ AGENT 4 testy (NOVÝ)
│   └── FairBank.Identity.UnitTests/               ← ★ AGENT 5 testy
│
├── docker-compose.yml                             ← NEUPRAVOVAT (jen Agent může přidat svůj service — viz pravidla)
├── docker/postgres/init.sql                       ← Přidej GRANT pro svůj schema
├── Directory.Build.props
└── Directory.Packages.props
```

## Docker Networking

```
                    ┌──────────────────────────────────────────────────────┐
    port 80         │  Docker network: backend (uzavřená síť)             │
  ┌──────────┐      │  ┌──────────┐  ┌────────────────┐                   │
  │ web-app  │──────│─▶│api-gateway│─▶│ identity-api   │                   │
  │ (nginx)  │ /api/│  │  (YARP)  │  │ accounts-api   │                   │
  └──────────┘      │  │          │  │ payments-api   │  ← Agent 2 přidá  │
  Jediný exposed    │  │          │  │ savings-api    │  ← Agent 3 přidá  │
  kontejner         │  │          │  │ investments-api│  ← Agent 4 přidá  │
                    │  └──────────┘  └────────────────┘                   │
                    │                 ┌──────────┐                        │
                    │                 │ postgres │ (všechna schémata)     │
                    │                 └──────────┘                        │
                    └──────────────────────────────────────────────────────┘
  Jedině web-app (port 80) je vystavený ven. Vše ostatní jen expose (bez ports).
```

## Existující backend vzory (reference)

### Hexagonální architektura — 4 vrstvy na službu

| Vrstva | Vzor | Příklad (Accounts) |
|--------|------|--------------------|
| **Domain** | Aggregáty, ValueObjects, Enumy, Port interfaces | `Account` (AggregateRoot), `Money` (VO), `IAccountEventStore` (port) |
| **Application** | MediatR CQRS Commands/Queries, FluentValidation, DTOs | `CreateAccountCommand` + Handler, `AccountResponse` DTO |
| **Infrastructure** | DB adaptéry (EF Core nebo Marten), Repository implementace | `MartenAccountEventStore`, `DependencyInjection.cs` |
| **Api** | Minimal API Endpoints, Program.cs (DI + middleware) | `AccountEndpoints.cs`, health check, OpenAPI/Scalar |

### Dvě persistence strategie

| Služba | Strategie | Schema | Technologie |
|--------|-----------|--------|-------------|
| Identity | CRUD | `identity_service` | EF Core + PostgreSQL |
| Accounts | Event sourcing | `accounts_service` | Marten + PostgreSQL |

Pro nové služby zvol CRUD (EF Core) — je jednodušší. Event sourcing jen pokud doménově dává smysl.

### Vzorový DependencyInjection.cs (infra)

```csharp
public static class DependencyInjection
{
    public static IServiceCollection Add<Service>Infrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MyDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "<schema_name>");
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            }));
        services.AddScoped<IMyRepository, MyRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MyDbContext>());
        return services;
    }
}
```

### Vzorový Program.cs (API)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());
builder.Services.Add<Service>Application();   // MediatR + FluentValidation
var cs = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing connection string.");
builder.Services.Add<Service>Infrastructure(cs);
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.MapOpenApi(); app.MapScalarApiReference(); }
app.UseSerilogRequestLogging();
app.Map<Entity>Endpoints();
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "<Name>" })).WithTags("Health");
app.Run();

public partial class Program;
```

### Vzorový Dockerfile (API služba)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/FairBank.SharedKernel/FairBank.SharedKernel.csproj src/FairBank.SharedKernel/
COPY src/Services/<Service>/FairBank.<Service>.Domain/FairBank.<Service>.Domain.csproj src/Services/<Service>/FairBank.<Service>.Domain/
COPY src/Services/<Service>/FairBank.<Service>.Application/FairBank.<Service>.Application.csproj src/Services/<Service>/FairBank.<Service>.Application/
COPY src/Services/<Service>/FairBank.<Service>.Infrastructure/FairBank.<Service>.Infrastructure.csproj src/Services/<Service>/FairBank.<Service>.Infrastructure/
COPY src/Services/<Service>/FairBank.<Service>.Api/FairBank.<Service>.Api.csproj src/Services/<Service>/FairBank.<Service>.Api/
RUN dotnet restore src/Services/<Service>/FairBank.<Service>.Api/FairBank.<Service>.Api.csproj
COPY src/FairBank.SharedKernel/ src/FairBank.SharedKernel/
COPY src/Services/<Service>/ src/Services/<Service>/
RUN dotnet publish src/Services/<Service>/FairBank.<Service>.Api/FairBank.<Service>.Api.csproj -c Release -o /app/publish

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.<Service>.Api.dll"]
```

### Vzorový test projekt (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Services/<Service>/FairBank.<Service>.Domain/FairBank.<Service>.Domain.csproj" />
    <ProjectReference Include="../../src/Services/<Service>/FairBank.<Service>.Application/FairBank.<Service>.Application.csproj" />
  </ItemGroup>
</Project>
```

---

## API Endpointy — existující + plánované

### Existující (funkční)

| Metoda | URL | Služba | Popis |
|--------|-----|--------|-------|
| `POST` | `/api/v1/users/register` | Identity | Registrace uživatele |
| `GET` | `/api/v1/users/{id}` | Identity | Detail uživatele |
| `POST` | `/api/v1/accounts` | Accounts | Vytvoření účtu |
| `GET` | `/api/v1/accounts/{id}` | Accounts | Detail účtu |
| `POST` | `/api/v1/accounts/{id}/deposit` | Accounts | Vklad |
| `POST` | `/api/v1/accounts/{id}/withdraw` | Accounts | Výběr |

### Nové — agenti vytvoří

| Metoda | URL | Agent | Popis |
|--------|-----|-------|-------|
| `POST` | `/api/v1/payments` | Agent 2 | Odeslání platby |
| `GET` | `/api/v1/payments?accountId={id}` | Agent 2 | Historie plateb |
| `GET` | `/api/v1/payments/{id}` | Agent 2 | Detail platby |
| `POST` | `/api/v1/savings/goals` | Agent 3 | Vytvoření spořícího cíle |
| `GET` | `/api/v1/savings/goals?ownerId={id}` | Agent 3 | Seznam cílů |
| `PUT` | `/api/v1/savings/goals/{id}` | Agent 3 | Aktualizace cíle |
| `POST` | `/api/v1/savings/goals/{id}/deposit` | Agent 3 | Vklad na cíl |
| `POST` | `/api/v1/savings/rules` | Agent 3 | Vytvoření pravidla |
| `GET` | `/api/v1/savings/rules?ownerId={id}` | Agent 3 | Seznam pravidel |
| `POST` | `/api/v1/investments` | Agent 4 | Nová investice |
| `GET` | `/api/v1/investments?ownerId={id}` | Agent 4 | Portfolio |
| `GET` | `/api/v1/investments/{id}` | Agent 4 | Detail investice |
| `POST` | `/api/v1/investments/{id}/buy` | Agent 4 | Koupit |
| `POST` | `/api/v1/investments/{id}/sell` | Agent 4 | Prodat |
| `PUT` | `/api/v1/users/{id}` | Agent 5 | Aktualizace profilu |
| `GET` | `/api/v1/users/{id}/accounts` | Agent 5 | Účty uživatele |

Frontend volání jdou přes `IFairBankApi` z `FairBank.Web.Shared/Services/`. Žádné zabezpečení — čisté open API.

---

## Design System — Va-bank

Téma je v `FairBank.Web.Shared/wwwroot/css/vabank-theme.css`. Barvy:

| Token | Hodnota | Použití |
|-------|---------|---------|
| `--vb-red` | `#C41E3A` | Primární akce, aktivní chip, CTA |
| `--vb-red-dark` | `#8B0000` | Hover, header gradient |
| `--vb-black` | `#1A1A1A` | Header bg, bottom nav, texty |
| `--vb-green` | `#27AE60` | Kladné částky, deposit |
| `--vb-gray-500` | `#888888` | Sekundární text, neaktivní |
| `--vb-white` | `#FFFFFF` | Karty, pozadí |

### Sdílené frontend komponenty (`FairBank.Web.Shared/Components/`)

| Komponenta | Props | Popis |
|------------|-------|-------|
| `PageHeader` | `Title` | Tmavý header s logem poker chipu |
| `BalanceCard` | `Amount`, `Currency` | Karta se zůstatkem |
| `ContentCard` | `Title`, `ChildContent`, `HeaderAction` | Bílá karta se záhlavím |
| `TransactionItem` | `Description`, `Date`, `Amount`, `Currency`, `Type` | Řádek transakce |
| `PokerChip` | `Value`, `IsActive`, `Size(sm/md/lg)` | Casino žeton (milestone) |
| `ProgressChips` | `Label`, `Percent`, `Chips` | Řada žetonů s progress barem |
| `ToggleSwitch` | `Label`, `IsOn`, `IsOnChanged` | Přepínač (on/off) |
| `VbButton` | `ChildContent`, `OnClick`, `Disabled`, `Variant(primary/secondary/outline/danger)` | Tlačítko |

---

## Agent 1 — Přehled účtu + Accounts služba

### Vlastnictví

| Vrstva | Projekty |
|--------|----------|
| **Backend** | `src/Services/Accounts/FairBank.Accounts.{Domain,Application,Infrastructure,Api}/` (existující — rozšířit) |
| **Frontend** | `src/FairBank.Web.Overview/` |
| **Testy** | `tests/FairBank.Accounts.UnitTests/` (existující — rozšířit) |

### Backend úkoly (Accounts service — už existuje, rozšířit)

1. **Seznam transakcí** — přidej `GET /api/v1/accounts/{id}/transactions` endpoint (query na event stream)
2. **Celkový zůstatek** — endpoint `GET /api/v1/accounts?ownerId={id}` — seznam účtů uživatele
3. **Endpoint validace** — přidej FluentValidation na existující commands kde chybí
4. **Error handling middleware** — `app.UseExceptionHandler` pro konzistentní JSON error responses
5. **EF Migrace** — pokud přidáváš projekci/snímek, aktualizuj Marten konfiguraci

### Frontend úkoly (Přehled — stránka `/` a `/prehled`)

6. **Dashboard zůstatku** — volání API, zobrazení v `BalanceCard`
7. **Seznam transakcí** — načtení a zobrazení pomocí `TransactionItem`
8. **Odkrýt/skrýt transakce** — toggle "Odkrýt" odkrývá/skrývá detaily
9. **FAB tlačítko** — plovoucí "+" button pro rychlý vklad/výběr (dialog/modal)
10. **Polokolový ukazatel** — vizualizace zůstatku (SVG semicircle gauge) jako na mockupu
11. **Loading / Error states** — skeleton loading, error handling

### Testy

12. **Domain testy** — rozšíř existující testy o novou funkcionalitu
13. **Application testy** — testy query handlerů (transactions listing)
14. **Validator testy** — FluentValidation unit testy

### Mockup reference
- Hlavní obrazovka: zůstatek uprostřed, pod ním seznam transakcí
- Header: tmavý gradient, "PŘEHLED ÚČTU", poker-chip kruhy

---

## Agent 2 — Platby (NOVÁ služba + frontend)

### Vlastnictví

| Vrstva | Projekty |
|--------|----------|
| **Backend** | `src/Services/Payments/FairBank.Payments.{Domain,Application,Infrastructure,Api}/` — **VYTVOŘIT** |
| **Frontend** | `src/FairBank.Web.Payments/` |
| **Testy** | `tests/FairBank.Payments.UnitTests/` — **VYTVOŘIT** |
| **Docker** | Přidat `payments-api` service do `docker-compose.yml` |
| **DB** | Schema `payments_service` — přidat do `docker/postgres/init.sql` |
| **Gateway** | Přidat route `/api/v1/payments/**` do `appsettings.json` API Gateway |

### Backend úkoly — NOVÁ služba od nuly

1. **Domain vrstva** — `Payment` agregát (Id, FromAccountId, ToAccountId, Amount, Currency, Description, Status, CreatedAt), `PaymentStatus` enum (Pending/Completed/Failed), `Money` VO (znovupoužij vzor z Accounts)
2. **Port interface** — `IPaymentRepository : IRepository<Payment, Guid>` + `GetByAccountIdAsync()`
3. **Application vrstva** — `SendPaymentCommand` + Handler + Validator, `GetPaymentByIdQuery`, `GetPaymentsByAccountQuery`, `PaymentResponse` DTO
4. **Infrastructure vrstva** — EF Core `PaymentsDbContext` (schema `payments_service`), `PaymentConfiguration`, `PaymentRepository`
5. **API vrstva** — Minimal API endpoints: `POST /api/v1/payments`, `GET /api/v1/payments/{id}`, `GET /api/v1/payments?accountId={id}`
6. **Dockerfile** — podle vzoru (viz výše)
7. **docker-compose.yml** — přidat `payments-api` service (expose 8080, backend network, depends_on postgres)
8. **API Gateway** — přidat route `payments-route` → `payments-cluster` (`http://payments-api:8080`)
9. **init.sql** — přidat `CREATE SCHEMA IF NOT EXISTS payments_service;` + GRANT

### Frontend úkoly (stránka `/platby`)

10. **Formulář nové platby** — příjemce, částka, popis, validace
11. **Odeslání platby** — volání `IFairBankApi` (rozšířit o payments metody)
12. **Historie plateb** — seznam odeslaných/přijatých plateb
13. **Šablony plateb** — uložené oblíbené příjemce
14. **Potvrzení platby** — confirmation dialog před odesláním
15. **Filtry** — filtrování historie podle data, částky

### Testy

16. **Domain testy** — Payment Create, validace, status transitions
17. **Application testy** — SendPaymentCommandHandler, query handlers
18. **Validator testy** — FluentValidation

### Mockup reference
- Formulář s poli: Příjemce, Částka (Kč suffix), Popis
- Pod formulářem historie s `TransactionItem` komponentami

---

## Agent 3 — Spoření, výzvy, cíle (NOVÁ služba + frontend)

### Vlastnictví

| Vrstva | Projekty |
|--------|----------|
| **Backend** | `src/Services/Savings/FairBank.Savings.{Domain,Application,Infrastructure,Api}/` — **VYTVOŘIT** |
| **Frontend** | `src/FairBank.Web.Savings/` |
| **Testy** | `tests/FairBank.Savings.UnitTests/` — **VYTVOŘIT** |
| **Docker** | Přidat `savings-api` service do `docker-compose.yml` |
| **DB** | Schema `savings_service` — přidat do `docker/postgres/init.sql` |
| **Gateway** | Přidat route `/api/v1/savings/**` do `appsettings.json` API Gateway |

### Backend úkoly — NOVÁ služba od nuly

1. **Domain vrstva** — `SavingsGoal` agregát (Id, OwnerId, Name, Description, TargetAmount, CurrentAmount, Currency, IsCompleted, CreatedAt), `SavingsRule` entita (Id, OwnerId, Name, Description, IsEnabled, RuleType), `RuleType` enum (RoundUp/FixedAmount/Percentage)
2. **Port interfaces** — `ISavingsGoalRepository`, `ISavingsRuleRepository`
3. **Application vrstva** — Commands: `CreateGoalCommand`, `DepositToGoalCommand`, `CreateRuleCommand`, `ToggleRuleCommand`; Queries: `GetGoalsByOwnerQuery`, `GetRulesByOwnerQuery`; DTOs: `SavingsGoalResponse`, `SavingsRuleResponse`
4. **Infrastructure vrstva** — EF Core `SavingsDbContext` (schema `savings_service`), entity configurations, repositories
5. **API vrstva** — Minimal API: `POST/GET /api/v1/savings/goals`, `POST /api/v1/savings/goals/{id}/deposit`, `POST/GET /api/v1/savings/rules`
6. **Dockerfile, docker-compose, API Gateway, init.sql** — jako Agent 2

### Frontend úkoly (stránka `/sporeni`)

7. **Finanční výzvy** — seznam cílů s poker-chip milestones
8. **Progress bary** — vizualizace postupu k cíli
9. **Vyzvat se** — CTA pro vytvoření nové výzvy
10. **Automatické spoření** — přehled pravidel
11. **Zaokrouhlování plateb** — toggle switch
12. **Pravidla spoření** — CRUD vlastních pravidel
13. **Historie spoření** — transakce spoření

### Testy

14. **Domain testy** — SavingsGoal (create, deposit, complete), SavingsRule
15. **Application testy** — command/query handlers
16. **Validator testy**

### Mockup reference
- Poker-chip milestones (200 Mil, 100 Mil, Va-bank)
- Červené = dosažené, šedé = nedosažené
- Toggle switch, progress bar pod chipy

---

## Agent 4 — Investice (NOVÁ služba + frontend)

### Vlastnictví

| Vrstva | Projekty |
|--------|----------|
| **Backend** | `src/Services/Investments/FairBank.Investments.{Domain,Application,Infrastructure,Api}/` — **VYTVOŘIT** |
| **Frontend** | `src/FairBank.Web.Investments/` |
| **Testy** | `tests/FairBank.Investments.UnitTests/` — **VYTVOŘIT** |
| **Docker** | Přidat `investments-api` service do `docker-compose.yml` |
| **DB** | Schema `investments_service` — přidat do `docker/postgres/init.sql` |
| **Gateway** | Přidat route `/api/v1/investments/**` do `appsettings.json` API Gateway |

### Backend úkoly — NOVÁ služba od nuly

1. **Domain vrstva** — `Investment` agregát (Id, OwnerId, Name, Type, InvestedAmount, CurrentValue, Currency, CreatedAt), `InvestmentType` enum (Fund/Bond/Crypto/Stock), `InvestmentTransaction` entita (Id, InvestmentId, Amount, Type, Date)
2. **Port interfaces** — `IInvestmentRepository`
3. **Application vrstva** — Commands: `CreateInvestmentCommand`, `BuyCommand`, `SellCommand`; Queries: `GetPortfolioQuery`, `GetInvestmentByIdQuery`; DTOs: `InvestmentResponse`, `PortfolioResponse`
4. **Infrastructure vrstva** — EF Core `InvestmentsDbContext` (schema `investments_service`), configurations, repository
5. **API vrstva** — Minimal API: `POST /api/v1/investments`, `GET /api/v1/investments?ownerId={id}`, `GET /api/v1/investments/{id}`, `POST /{id}/buy`, `POST /{id}/sell`
6. **Dockerfile, docker-compose, API Gateway, init.sql** — jako Agent 2

### Frontend úkoly (stránka `/investice`)

7. **Portfolio přehled** — celková hodnota v `BalanceCard`
8. **Seznam investic** — fondy, kryptoměny s hodnotou a % změnou
9. **Koupit/prodat** — tlačítka pro investiční operace
10. **Investiční příležitosti** — doporučené investice
11. **Risk profil** — poker chips = risk levels
12. **Empty state** — poker chip s "?" a CTA

### Testy

13. **Domain testy** — Investment (create, buy, sell, valuation)
14. **Application testy** — command/query handlers
15. **Validator testy**

### Mockup reference
- Karty s názvem fondu, typem, hodnotou a % změnou
- Progress bar pro výkon

---

## Agent 5 — Profil + Identity služba

### Vlastnictví

| Vrstva | Projekty |
|--------|----------|
| **Backend** | `src/Services/Identity/FairBank.Identity.{Domain,Application,Infrastructure,Api}/` (existující — rozšířit) |
| **Frontend** | `src/FairBank.Web.Profile/` |
| **Testy** | `tests/FairBank.Identity.UnitTests/` (existující — rozšířit) |

### Backend úkoly (Identity service — už existuje, rozšířit)

1. **Update profilu** — přidej `UpdateUserCommand` + Handler + Validator, endpoint `PUT /api/v1/users/{id}`
2. **Účty uživatele** — endpoint `GET /api/v1/users/{id}/accounts` (cross-service query — nebo proxy přes gateway)
3. **Soft Delete** — endpoint `DELETE /api/v1/users/{id}` (volá `user.SoftDelete()`, už existuje v doméně)
4. **User settings** — přidej `UserSettings` entitu (pushNotifications, darkMode, biometricLogin), CRUD endpoints
5. **Password change** — `ChangePasswordCommand` endpoint

### Frontend úkoly (stránka `/profil`)

6. **Profil avatar** — velký poker chip s iniciálami uživatele
7. **Osobní údaje** — jméno, příjmení, email, role (z API)
8. **Nastavení** — toggle switches (push, dark mode, biometrika)
9. **Info o účtu** — číslo účtu, stav, datum vytvoření
10. **Editace údajů** — formulář pro úpravu profilu
11. **Odhlášení** — danger button

### Testy

12. **Domain testy** — rozšíř o UpdateUser, ChangePassword testy
13. **Application testy** — UpdateUserCommandHandler, ChangePasswordHandler
14. **Validator testy**

### Mockup reference
- Velký červený poker chip s iniciálami jako avatar
- Info řádky, toggle switches, "Odhlásit se" button

---

## Globální pravidla pro VŠECHNY agenty

### ✅ MUSÍŠ

1. **Pracovat POUZE ve svých projektech** (viz tabulka vlastnictví výše)
2. **Dodržovat hexagonální architekturu** — Domain nemá žádné externí závislosti (kromě SharedKernel), Application závisí jen na Domain, Infrastructure implementuje porty
3. **Používat existující vzory** — zkopíruj strukturu z Identity/Accounts služby
4. **Registrovat DI** — `Add<Service>Application()` (MediatR + FluentValidation) a `Add<Service>Infrastructure(connectionString)` v Program.cs
5. **Přidat health endpoint** — `app.MapGet("/health", ...)` v API
6. **Namespace konvence**: `FairBank.<Service>.<Layer>` (backend), `FairBank.Web.<Modul>.Pages` (frontend)
7. **Používat sdílené frontend komponenty** z `FairBank.Web.Shared/Components/`
8. **Minimálně 5 unit testů** na Domain + 3 na Application v testech
9. **PostgreSQL schema** — každá služba má vlastní: `<service>_service`
10. **Dockerfile** — follow vzor výše, EXPOSE 8080

### ❌ NESMÍŠ

1. **Upravovat `FairBank.Web/`** (App.razor, MainLayout, BottomNav, Program.cs) — kromě přidání assembly reference v App.razor pokud vytváříš nový frontend modul
2. **Upravovat `FairBank.SharedKernel/`** — je zamrzlý
3. **Upravovat projekty JINÉHO agenta** — viz vlastnictví
4. **Měnit existující API kontrakty** — jen přidávat nové endpointy
5. **Používat `ports` v docker-compose** pro backend služby — jen `expose`
6. **Odstraňovat existující kód** bez náhrady

### 🔧 SMÍŠ (sdílené zdroje — koordinace)

Tyto soubory může upravit VÍCE agentů — přidávej na konec, neupravuj existující řádky:

| Soubor | Jak upravit |
|--------|-------------|
| `docker-compose.yml` | Přidej svůj service blok **pod** existující, před `networks:` |
| `docker/postgres/init.sql` | Přidej `CREATE SCHEMA` + `GRANT` **na konec** souboru |
| `src/FairBank.ApiGateway/appsettings.json` | Přidej svůj route + cluster **do** existujících objektů |
| `src/FairBank.ApiGateway/appsettings.Development.json` | Přidej localhost cluster |
| `FairBank.slnx` | Přidej své projekty do odpovídajícího `<Folder>` |
| `Directory.Packages.props` | Přidej nové PackageVersion pokud potřebuješ balíček |
| `src/FairBank.Web.Shared/Services/IFairBankApi.cs` | Přidej nové metody **na konec** interface |
| `src/FairBank.Web.Shared/Services/FairBankApiClient.cs` | Přidej implementaci **na konec** třídy |
| `src/FairBank.Web.Shared/Models/` | Přidej nové model soubory |
| `src/FairBank.Web.Shared/Components/` | Přidej nové komponenty |
| `src/FairBank.Web.Shared/wwwroot/css/vabank-theme.css` | Přidej CSS **na konec** souboru |

### Build & Test

```bash
# Build celé řešení
dotnet build FairBank.slnx

# Build jen svůj backend
dotnet build src/Services/<Service>/FairBank.<Service>.Api/FairBank.<Service>.Api.csproj

# Build jen svůj frontend modul
dotnet build src/FairBank.Web.<Modul>/FairBank.Web.<Modul>.csproj

# Spustit testy
dotnet test tests/FairBank.<Service>.UnitTests/

# Docker — celý stack
docker compose up --build

# Docker — jen svou službu
docker compose up --build <service-name>-api
```

### Checklist před dokončením

- [ ] `dotnet build FairBank.slnx` — 0 errors, 0 warnings
- [ ] `dotnet test` — všechny testy prochází
- [ ] Backend API vrací správné HTTP status kódy (201, 200, 404, 400)
- [ ] Frontend stránka se renderuje a volá API
- [ ] Dockerfile builduje
- [ ] Health endpoint `/health` odpovídá
- [ ] Schema v init.sql s GRANTy
