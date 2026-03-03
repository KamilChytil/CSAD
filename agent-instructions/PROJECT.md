# FairBank - Modern Digital Banking Application

> **For AI Agents:** This document describes the complete state of the FairBank project as of 2026-03-03. Read this before making any changes.

## Table of Contents

1. [Project Overview](#project-overview)
2. [Technology Stack](#technology-stack)
3. [Project Structure](#project-structure)
4. [Architecture](#architecture)
5. [SharedKernel](#sharedkernel)
6. [Identity Service](#identity-service)
7. [Accounts Service](#accounts-service)
8. [API Gateway](#api-gateway)
9. [Database Strategy](#database-strategy)
10. [Docker & Deployment](#docker--deployment)
11. [Testing](#testing)
12. [API Endpoints](#api-endpoints)
13. [How To Run / Build / Test](#how-to-run--build--test)
14. [Conventions & Patterns](#conventions--patterns)
15. [Known Gotchas & Pitfalls](#known-gotchas--pitfalls)
16. [Planned Future Services](#planned-future-services)

---

## Project Overview

FairBank is a microservices-based digital banking application built as a university project. It implements:

- **Identity Service** — User registration, authentication (EF Core + PostgreSQL)
- **Accounts Service** — Bank account management with event sourcing (Marten + PostgreSQL)
- **API Gateway** — YARP reverse proxy routing to backend services

The application follows **Hexagonal Architecture** (Ports & Adapters) with **Domain-Driven Design (DDD)**, **CQRS**, and **Event Sourcing** patterns.

**Repository:** https://github.com/KamilChytil/CSAD.git
**Branch:** `main`

---

## Technology Stack

| Category | Technology | Version |
|----------|-----------|---------|
| Runtime | .NET | 10.0 (LTS) |
| Language | C# | 14 |
| Solution format | slnx | `FairBank.slnx` (NOT .sln) |
| CQRS/Mediator | MediatR | 14.0.0 |
| Validation | FluentValidation | 12.1.1 |
| ORM (Identity) | EF Core + Npgsql | 10.0.3 / 10.0.0 |
| Event Store (Accounts) | Marten | 8.22.1 |
| API Docs | Microsoft.AspNetCore.OpenApi + Scalar | 10.0.3 / 2.4.12 |
| API Gateway | YARP | 2.3.0 |
| Logging | Serilog | 10.0.0 |
| Database | PostgreSQL | 16 (Alpine) |
| Container | Docker Compose | Multi-stage Alpine builds |
| Testing | xUnit + FluentAssertions + NSubstitute | 2.9.3 / 8.8.0 / 5.3.0 |
| Package Management | Central Package Management (CPM) | Directory.Packages.props |

---

## Project Structure

```
FairBank/
├── FairBank.slnx                          # Solution file (.NET 10 slnx format)
├── Directory.Build.props                   # Shared build properties (net10.0, nullable, warnings-as-errors)
├── Directory.Packages.props                # Central Package Management - ALL versions defined here
├── docker-compose.yml                      # Full orchestration
├── docker/
│   └── postgres/
│       └── init.sql                        # Creates schemas + app user
├── docs/
│   └── plans/
│       └── 2026-03-03-api-and-database-foundation.md
├── agent-instructions/
│   └── PROJECT.md                          # This file
├── src/
│   ├── FairBank.SharedKernel/              # DDD base classes (Entity, AggregateRoot, ValueObject)
│   ├── FairBank.ApiGateway/                # YARP reverse proxy
│   └── Services/
│       ├── Identity/
│       │   ├── FairBank.Identity.Domain/           # User aggregate, Email VO, UserRole enum
│       │   ├── FairBank.Identity.Application/      # RegisterUser command, GetUserById query
│       │   ├── FairBank.Identity.Infrastructure/   # EF Core DbContext, UserRepository, migrations
│       │   └── FairBank.Identity.Api/              # Minimal API endpoints, Program.cs
│       └── Accounts/
│           ├── FairBank.Accounts.Domain/           # Account aggregate (event-sourced), Money VO, AccountNumber VO
│           ├── FairBank.Accounts.Application/      # CreateAccount, Deposit, Withdraw commands
│           ├── FairBank.Accounts.Infrastructure/   # Marten event store adapter
│           └── FairBank.Accounts.Api/              # Minimal API endpoints, Program.cs
└── tests/
    ├── FairBank.Identity.UnitTests/        # 20 tests
    └── FairBank.Accounts.UnitTests/        # 20 tests
```

**Total: 12 projects, 40 unit tests, 0 warnings, 0 errors.**

---

## Architecture

### Hexagonal Architecture (per service)

```
                    ┌─────────────────────────────────────┐
                    │              API Layer               │
                    │   (Minimal APIs, Program.cs)         │
                    └──────────────┬──────────────────────┘
                                   │
                    ┌──────────────▼──────────────────────┐
                    │         Application Layer            │
                    │  (Commands, Queries, Handlers,       │
                    │   Validators, DTOs)                  │
                    │  Depends on: Domain                  │
                    └──────────────┬──────────────────────┘
                                   │
                    ┌──────────────▼──────────────────────┐
                    │           Domain Layer               │
                    │  (Aggregates, Value Objects, Enums,  │
                    │   Domain Events, Port Interfaces)    │
                    │  NO external dependencies            │
                    └──────────────┬──────────────────────┘
                                   │ (implements ports)
                    ┌──────────────▼──────────────────────┐
                    │       Infrastructure Layer           │
                    │  (DbContext, Repositories,           │
                    │   Event Store, Migrations)           │
                    └─────────────────────────────────────┘
```

### Dependency Flow

- **Domain** → depends on nothing (only SharedKernel)
- **Application** → depends on Domain
- **Infrastructure** → depends on Domain + Application (implements ports)
- **Api** → depends on Application + Infrastructure (composes everything)

### CQRS Pattern

Every operation is either a **Command** (write) or **Query** (read), dispatched via MediatR `ISender`:

```
Request → ISender.Send() → IRequestHandler → Repository/EventStore → Response
```

Commands and queries live in: `Application/{Feature}/Commands/` and `Application/{Feature}/Queries/`

Each command/query folder contains:
- `{Name}Command.cs` or `{Name}Query.cs` — the request record
- `{Name}CommandHandler.cs` or `{Name}QueryHandler.cs` — the handler
- `{Name}CommandValidator.cs` — FluentValidation rules (commands only, optional)

---

## SharedKernel

**Project:** `src/FairBank.SharedKernel/FairBank.SharedKernel.csproj`
**Namespace:** `FairBank.SharedKernel.Domain`, `FairBank.SharedKernel.Application`

Base DDD building blocks used by all services:

| Class | Purpose | Key Details |
|-------|---------|-------------|
| `Entity<TId>` | Base entity | Equality by `Id`, generic type constraint `TId : notnull` |
| `AggregateRoot<TId>` | Aggregate root | Extends Entity, tracks `DomainEvents` list, `RaiseDomainEvent()`, `ClearDomainEvents()` |
| `ValueObject` | Value object base | Abstract `GetAtomicValues()`, equality by value comparison |
| `IDomainEvent` | Event interface | Extends MediatR `INotification` |
| `DomainEvent` | Event base record | Auto-generates `EventId` (Guid) and `OccurredAt` (UTC) |
| `IRepository<TAggregate, TId>` | Repository port | `GetByIdAsync`, `AddAsync`, `UpdateAsync` |
| `IUnitOfWork` | Unit of Work | `SaveChangesAsync` |

---

## Identity Service

### Domain Layer

**Aggregate: `User : AggregateRoot<Guid>`**
- Factory: `User.Create(firstName, lastName, email, passwordHash, role)` — validates, trims, sets `IsActive=true`, `IsDeleted=false`
- Methods: `SoftDelete()`, `Restore()`
- Private constructor for EF Core rehydration

**Value Object: `Email : ValueObject`**
- Factory: `Email.Create(string email)` — regex validation (`^[^@\s]+@[^@\s]+\.[^@\s]+$`), lowercased, trimmed
- Uses `[GeneratedRegex]` for compile-time regex

**Enum: `UserRole`** — `Client=0, Child=1, Banker=2, Admin=3`

**Port: `IUserRepository : IRepository<User, Guid>`**
- Additional: `GetByEmailAsync(Email)`, `ExistsWithEmailAsync(Email)`

### Application Layer

**Command: `RegisterUserCommand(FirstName, LastName, Email, Password, Role)`**
- Handler: Checks duplicate email → SHA256 hash password → `User.Create()` → save
- Validator: FirstName/LastName not empty (max 100), valid email, password min 8 chars with uppercase + lowercase + digit + special char
- **Note:** SHA256 is a placeholder. Production must use BCrypt/Argon2.

**Query: `GetUserByIdQuery(Guid Id)`**
- Handler: Loads user by ID → maps to `UserResponse` or returns null

**DTO: `UserResponse(Id, FirstName, LastName, Email, Role, IsActive, CreatedAt)`**

### Infrastructure Layer

**`IdentityDbContext : DbContext, IUnitOfWork`**
- Default schema: `identity_service`
- Table: `users`
- Email as owned value object (`OwnsOne`)
- Role stored as string
- **Global query filter:** `builder.HasQueryFilter(u => !u.IsDeleted)` — soft deleted users are automatically excluded
- Unique index on email
- Retry on failure: 3 attempts

**`UserRepository : IUserRepository`** — Standard EF Core implementation

**EF Core Migration:** `20260303101715_InitialCreate` in `identity_service` schema

### DI Registration

```csharp
// In Program.cs:
builder.Services.AddIdentityApplication();     // MediatR + FluentValidation
builder.Services.AddIdentityInfrastructure(connectionString);  // DbContext + Repository
```

---

## Accounts Service

### Domain Layer

**Aggregate: `Account`** (does NOT inherit AggregateRoot — uses Marten event sourcing directly)
- Factory: `Account.Create(ownerId, currency)` — zero balance, auto-generated account number
- Methods: `Deposit(Money, description)`, `Withdraw(Money, description)`, `Deactivate()`
- Event rehydration: `Apply(AccountCreated)`, `Apply(MoneyDeposited)`, `Apply(MoneyWithdrawn)`
- Tracks `_uncommittedEvents` list internally
- Private parameterless constructor for Marten deserialization

**Value Object: `Money : ValueObject`**
- Properties: `Amount` (decimal, rounded 2dp), `Currency`
- Factory: `Money.Create(amount, currency)` — rejects negative amounts
- Static: `Money.Zero(currency)`
- Methods: `Add(Money)`, `Subtract(Money)` — enforce same currency, insufficient funds check

**Value Object: `AccountNumber : ValueObject`**
- Format: `FAIR-XXXX-XXXX-XXXX` (random 4-digit segments)
- Factory: `AccountNumber.Create(value?)` — generates if null

**Enum: `Currency`** — `CZK, EUR, USD, GBP`

**Domain Events (sealed records):**
- `AccountCreated(AccountId, OwnerId, AccountNumber, Currency, OccurredAt)`
- `MoneyDeposited(AccountId, Amount, Currency, Description, OccurredAt)`
- `MoneyWithdrawn(AccountId, Amount, Currency, Description, OccurredAt)`

### Application Layer

**Port: `IAccountEventStore`**
- `LoadAsync(Guid accountId)` — rehydrates aggregate from events
- `AppendEventsAsync(Account account)` — persists uncommitted events

**Commands:**
- `CreateAccountCommand(OwnerId, Currency)` → handler creates Account, appends events
- `DepositMoneyCommand(AccountId, Amount, Currency, Description)` → loads account, deposits, appends
- `WithdrawMoneyCommand(AccountId, Amount, Currency, Description)` → loads account, withdraws, appends

**Query: `GetAccountByIdQuery(AccountId)`**
- Loads from event store, maps to `AccountResponse`

**DTO: `AccountResponse(Id, OwnerId, AccountNumber, Balance, Currency, IsActive, CreatedAt)`**

### Infrastructure Layer

**`MartenAccountEventStore : IAccountEventStore`**
- Uses `IDocumentSession` from Marten
- `LoadAsync` → `session.Events.AggregateStreamAsync<Account>(accountId)`
- `AppendEventsAsync` → `session.Events.StartStream<Account>(id, events)` + `SaveChangesAsync()`

**Marten Configuration:**
```csharp
services.AddMarten(options => {
    options.Connection(connectionString);
    options.DatabaseSchemaName = "accounts_service";
    options.Events.DatabaseSchemaName = "accounts_service";
    options.AutoCreateSchemaObjects = AutoCreate.All;  // using JasperFx; (NOT Weasel.Core)
    options.Projections.Snapshot<Account>(SnapshotLifecycle.Inline);
}).UseLightweightSessions();
```

### DI Registration

```csharp
// In Program.cs:
builder.Services.AddAccountsApplication();     // MediatR + FluentValidation
builder.Services.AddAccountsInfrastructure(connectionString);  // Marten + EventStore
```

---

## API Gateway

**Project:** `src/FairBank.ApiGateway/FairBank.ApiGateway.csproj`

YARP reverse proxy that routes all traffic to backend services.

### Routes

| Route | Path Pattern | Target Cluster |
|-------|-------------|----------------|
| `identity-route` | `/api/v1/users/{**catch-all}` | `identity-cluster` |
| `accounts-route` | `/api/v1/accounts/{**catch-all}` | `accounts-cluster` |
| `identity-health` | `/identity/health` | `identity-cluster` (strips `/identity` prefix) |
| `accounts-health` | `/accounts/health` | `accounts-cluster` (strips `/accounts` prefix) |

### Clusters

| Cluster | Docker Address | Dev Address |
|---------|---------------|-------------|
| `identity-cluster` | `http://identity-api:8080` | `http://localhost:8001` |
| `accounts-cluster` | `http://accounts-api:8080` | `http://localhost:8002` |

Development overrides are in `appsettings.Development.json`.

---

## Database Strategy

### Single PostgreSQL Instance, Multiple Schemas

```
PostgreSQL 16 (fairbank database)
├── identity_service schema  ← EF Core (Identity Service)
│   └── users table
│   └── __EFMigsHistory table
└── accounts_service schema  ← Marten (Accounts Service)
    └── mt_streams table (event streams)
    └── mt_events table (events)
    └── mt_doc_account table (inline snapshots)
```

### Users

| User | Password | Purpose |
|------|----------|---------|
| `fairbank_admin` | `fairbank_secret_2026` | PostgreSQL superuser (Docker) |
| `fairbank_app` | `fairbank_app_2026` | Application user (limited privileges) |

### Connection String Pattern

```
Host=postgres;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path={schema_name}
```

---

## Docker & Deployment

### Services

| Service | Container | Port (host:container) | Dockerfile |
|---------|-----------|----------------------|------------|
| PostgreSQL | `fairbank-postgres` | `5432:5432` | postgres:16-alpine |
| Identity API | `fairbank-identity-api` | `8001:8080` | `src/Services/Identity/FairBank.Identity.Api/Dockerfile` |
| Accounts API | `fairbank-accounts-api` | `8002:8080` | `src/Services/Accounts/FairBank.Accounts.Api/Dockerfile` |
| API Gateway | `fairbank-api-gateway` | `5000:8080` | `src/FairBank.ApiGateway/Dockerfile` |

### Dockerfile Pattern (all services follow this)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/{project paths}/*.csproj src/{project paths}/
RUN dotnet restore src/{api project}/{api project}.csproj
COPY src/ src/
RUN dotnet publish src/{api project}/{api project}.csproj -c Release -o /app/publish

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "{assembly}.dll"]
```

**IMPORTANT:** Do NOT use `--no-restore` on `dotnet publish` in Dockerfiles. The `COPY src/ src/` step overwrites the `obj/` directory from restore, causing NETSDK1064 errors.

### Build & Run

```bash
# From repository root:
docker compose build
docker compose up -d

# Verify:
curl http://localhost:5000/health              # Gateway health
curl http://localhost:5000/identity/health      # Identity via gateway
curl http://localhost:5000/accounts/health      # Accounts via gateway
```

### Health Checks

Every service exposes `GET /health` returning:
```json
{ "Status": "Healthy", "Service": "{ServiceName}" }
```

---

## Testing

### Test Projects

| Project | Tests | Covers |
|---------|-------|--------|
| `FairBank.Identity.UnitTests` | 20 | Domain (User, Email) + Application (RegisterUser handler/validator) |
| `FairBank.Accounts.UnitTests` | 20 | Domain (Account, Money) + Application (Create/Deposit/Withdraw handlers) |

### Test Conventions

- **Framework:** xUnit with `<Using Include="Xunit" />` in csproj
- **Assertions:** FluentAssertions (`result.Should().BeTrue()`, `.BeEquivalentTo()`)
- **Mocking:** NSubstitute (`Substitute.For<IUserRepository>()`)
- **Naming:** `MethodName_Scenario_ExpectedResult`
- **No Version attributes** on PackageReference in test csproj files (CPM handles versions)

### Running Tests

```bash
dotnet test             # All tests
dotnet test --filter "FairBank.Identity"    # Identity tests only
dotnet test --filter "FairBank.Accounts"    # Accounts tests only
```

### Test Structure Example

```csharp
// Domain test
[Fact]
public void Create_WithValidData_ShouldCreateUser()
{
    var user = User.Create("Jan", "Novák", Email.Create("jan@example.com"), "hash", UserRole.Client);
    user.FirstName.Should().Be("Jan");
    user.IsActive.Should().BeTrue();
}

// Handler test with NSubstitute
[Fact]
public async Task Handle_WithValidCommand_ShouldCreateUser()
{
    var repo = Substitute.For<IUserRepository>();
    var uow = Substitute.For<IUnitOfWork>();
    repo.ExistsWithEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);

    var handler = new RegisterUserCommandHandler(repo, uow);
    var command = new RegisterUserCommand("Jan", "Novák", "jan@example.com", "Password1!", UserRole.Client);

    var result = await handler.Handle(command, CancellationToken.None);

    result.FirstName.Should().Be("Jan");
    await repo.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
}
```

---

## API Endpoints

### Identity Service (port 8001, or via gateway at /api/v1/users)

| Method | Path | Description | Request Body | Response |
|--------|------|-------------|-------------|----------|
| POST | `/api/v1/users/register` | Register a new user | `RegisterUserCommand` | 201 + `UserResponse` |
| GET | `/api/v1/users/{id:guid}` | Get user by ID | — | 200 + `UserResponse` or 404 |

### Accounts Service (port 8002, or via gateway at /api/v1/accounts)

| Method | Path | Description | Request Body | Response |
|--------|------|-------------|-------------|----------|
| POST | `/api/v1/accounts` | Create account | `CreateAccountCommand` | 201 + `AccountResponse` |
| GET | `/api/v1/accounts/{id:guid}` | Get account by ID | — | 200 + `AccountResponse` or 404 |
| POST | `/api/v1/accounts/{id:guid}/deposit` | Deposit money | `DepositMoneyCommand` | 200 + `AccountResponse` |
| POST | `/api/v1/accounts/{id:guid}/withdraw` | Withdraw money | `WithdrawMoneyCommand` | 200 + `AccountResponse` |

### OpenAPI / Scalar

In Development mode, each service exposes:
- `GET /openapi/v1.json` — OpenAPI spec
- `GET /scalar/v1` — Scalar interactive API reference

**Note:** Swashbuckle/Swagger is NOT available in .NET 10. Use `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` instead.

---

## How To Run / Build / Test

### Prerequisites

- .NET 10 SDK
- Docker & Docker Compose
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

### Build Solution

```bash
dotnet build FairBank.slnx
```

### Run Tests

```bash
dotnet test FairBank.slnx
```

### Run with Docker Compose

```bash
docker compose build
docker compose up -d

# Check logs:
docker compose logs -f identity-api
docker compose logs -f accounts-api
```

### Run Locally (without Docker)

Start PostgreSQL first, then:

```bash
# Terminal 1: Identity API
cd src/Services/Identity/FairBank.Identity.Api
dotnet run

# Terminal 2: Accounts API
cd src/Services/Accounts/FairBank.Accounts.Api
dotnet run

# Terminal 3: API Gateway
cd src/FairBank.ApiGateway
dotnet run
```

### EF Core Migrations (Identity)

```bash
# Ensure dotnet-ef is on PATH
export PATH="$PATH:$HOME/.dotnet/tools"

# Add migration:
dotnet ef migrations add MigrationName \
  --project src/Services/Identity/FairBank.Identity.Infrastructure \
  --startup-project src/Services/Identity/FairBank.Identity.Api

# Apply migration:
dotnet ef database update \
  --project src/Services/Identity/FairBank.Identity.Infrastructure \
  --startup-project src/Services/Identity/FairBank.Identity.Api
```

**Marten (Accounts)** manages its own schema automatically via `AutoCreate.All`.

---

## Conventions & Patterns

### Code Conventions

1. **Sealed classes/records everywhere** — all commands, queries, handlers, DTOs, domain events
2. **Records for DTOs and commands** — immutable data carriers
3. **Private setters on aggregates** — domain state changes only through methods
4. **Factory methods** (`Create()`) — never use constructors directly for aggregates/VOs
5. **Private constructors** — for EF Core / Marten deserialization only
6. **CancellationToken** on every async method (`ct = default`)
7. **Extension methods for DI** — `AddIdentityApplication()`, `AddIdentityInfrastructure(connectionString)`

### Naming Conventions

| Item | Convention | Example |
|------|-----------|---------|
| Commands | `{Verb}{Noun}Command` | `RegisterUserCommand` |
| Queries | `Get{Noun}By{Field}Query` | `GetUserByIdQuery` |
| Handlers | `{CommandName}Handler` | `RegisterUserCommandHandler` |
| Validators | `{CommandName}Validator` | `RegisterUserCommandValidator` |
| DTOs | `{Noun}Response` | `UserResponse` |
| Domain Events | `{Noun}{PastTenseVerb}` | `AccountCreated`, `MoneyDeposited` |
| Ports | `I{Noun}Repository` or `I{Noun}EventStore` | `IUserRepository`, `IAccountEventStore` |
| DbContext | `{Service}DbContext` | `IdentityDbContext` |
| Endpoints | `{Noun}Endpoints` | `UserEndpoints`, `AccountEndpoints` |

### Adding a New Service (Workflow)

1. Create 4 projects under `src/Services/{ServiceName}/`:
   - `FairBank.{Service}.Domain` — entities, VOs, enums, ports
   - `FairBank.{Service}.Application` — commands, queries, handlers, DTOs
   - `FairBank.{Service}.Infrastructure` — DB implementation
   - `FairBank.{Service}.Api` — endpoints, Program.cs, Dockerfile
2. Create test project: `tests/FairBank.{Service}.UnitTests`
3. Add all projects to `FairBank.slnx`
4. Add new packages to `Directory.Packages.props` (no Version in csproj!)
5. Create DI extension methods in Application and Infrastructure layers
6. Wire up in API's `Program.cs`
7. Add Dockerfile (follow existing pattern)
8. Add to `docker-compose.yml`
9. Add routes to API Gateway `appsettings.json`
10. Add schema to `docker/postgres/init.sql`

### Adding a New Command (Workflow)

1. Create folder: `Application/{Feature}/Commands/{CommandName}/`
2. Create `{Name}Command.cs` — sealed record implementing `IRequest<{Response}>`
3. Create `{Name}CommandHandler.cs` — sealed class implementing `IRequestHandler<{Command}, {Response}>`
4. (Optional) Create `{Name}CommandValidator.cs` — sealed class extending `AbstractValidator<{Command}>`
5. MediatR auto-discovers handlers via `RegisterServicesFromAssembly`
6. FluentValidation auto-discovers validators via `AddValidatorsFromAssembly`
7. Add endpoint in `{Noun}Endpoints.cs`

---

## Known Gotchas & Pitfalls

### .NET 10 Specific

1. **No Swashbuckle/Swagger** — Swashbuckle was removed from .NET 10. Use `Microsoft.AspNetCore.OpenApi` (`AddOpenApi()`, `MapOpenApi()`) + `Scalar.AspNetCore` (`MapScalarApiReference()`) instead.
2. **Solution format is `.slnx`** — NOT `.sln`. Use `dotnet build FairBank.slnx`.
3. **No `--no-restore` in Dockerfiles** — `COPY src/ src/` overwrites the `obj/` directory, invalidating restore cache. Always let publish do its own restore.

### Marten Specific

4. **`AutoCreate` moved namespace** — In Marten 8.22.1, `AutoCreate.All` requires `using JasperFx;` (NOT `using Weasel.Core;`). This is a breaking change from earlier Marten versions.
5. **Account aggregate doesn't extend AggregateRoot** — Marten's event sourcing uses its own rehydration pattern (`Apply` methods + private parameterless constructor). Don't make event-sourced aggregates inherit from `AggregateRoot<TId>`.

### Central Package Management (CPM)

6. **Never put Version on PackageReference** — All versions are in `Directory.Packages.props`. If you `dotnet add package`, it adds a Version attribute — you must remove it manually.
7. **`dotnet new` templates conflict with CPM** — Templates like `dotnet new xunit` generate csproj with Version attributes. Rewrite the csproj to remove them.

### Docker

8. **Docker build context is repository root** — All Dockerfiles use `context: .` in docker-compose. Paths in Dockerfile are relative to repo root.
9. **Non-root user in containers** — All services run as `appuser:appgroup` (uid 1000). Keep this in mind for file permissions.

### EF Core

10. **Migrations need both projects** — `--project` (Infrastructure) and `--startup-project` (Api). The Infrastructure project has Design package, the Api project has the connection string.
11. **Soft delete global filter** — Users with `IsDeleted=true` are invisible to normal queries. Use `IgnoreQueryFilters()` if you need to access them.

### General

12. **SHA256 password hashing is temporary** — Must be replaced with BCrypt or Argon2 before production.
13. **`dotnet-ef` tool path** — After installing globally, you may need `export PATH="$PATH:$HOME/.dotnet/tools"`.
14. **Backslash paths in git** — EF Core design-time tooling can create directories with literal `\` characters on Linux. The `.gitignore` has `**/bin\\*` to catch these.

---

## Planned Future Services

Based on the design document, these services are planned but NOT yet implemented:

| Service | Purpose | Persistence |
|---------|---------|-------------|
| Payments | Inter-account transfers, card payments, fraud detection | Event Sourcing (Marten) |
| Loans | Loan origination, amortization, interest calculation | EF Core |
| Children | Child accounts with parental controls, limits, rewards | EF Core |
| Notifications | Email/push notifications for transactions, alerts | Event-driven |
| Documents | PDF/Excel/DOCX generation (statements, contracts) | File storage |
| Chat | Real-time messaging between clients and bankers | SignalR + storage |

Each should follow the same hexagonal architecture pattern established by Identity and Accounts services.
