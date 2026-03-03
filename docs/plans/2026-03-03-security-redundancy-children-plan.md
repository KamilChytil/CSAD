# DB Redundancy, Child Accounts & Spending Limits — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix Marten event store bug, add PostgreSQL primary-replica replication, implement child accounts with parental oversight (ParentId, CreateChild, GetChildren), add spending limits and pending transaction approval flow in Accounts, and extend the frontend API client.

**Architecture:** PostgreSQL primary-replica streaming replication in Docker Compose. Parent-child self-reference FK in Identity domain. Spending limits and PendingTransaction event-sourced aggregate in Accounts domain. No auth enforcement — all endpoints open for easy testing. BCrypt for password hashing.

**Tech Stack:** BCrypt.Net-Next, PostgreSQL 16 streaming replication, Marten 8.22.1 (event sourcing), EF Core 10, MediatR 14.

> **Scope exclusions (deferred):** JWT authentication, Session/RefreshToken entities, auth endpoints (login/logout/register/session-validation), API Gateway security (JWT validation, RBAC, rate limiting, CORS), audit log. These will be added in a future phase.

---

### Task 1: Fix Marten EventStore Bug (StartStream vs AppendToStream)

**Files:**
- Modify: `src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/MartenAccountEventStore.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Application/Ports/IAccountEventStore.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Application/Commands/CreateAccount/CreateAccountCommandHandler.cs`

**Step 1: Update `IAccountEventStore` — add `StartStreamAsync`**

Replace `src/Services/Accounts/FairBank.Accounts.Application/Ports/IAccountEventStore.cs`:

```csharp
using FairBank.Accounts.Domain.Aggregates;

namespace FairBank.Accounts.Application.Ports;

public interface IAccountEventStore
{
    Task<Account?> LoadAsync(Guid accountId, CancellationToken ct = default);
    Task StartStreamAsync(Account account, CancellationToken ct = default);
    Task AppendEventsAsync(Account account, CancellationToken ct = default);
}
```

**Step 2: Fix `MartenAccountEventStore` — separate StartStream and Append**

Replace `src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/MartenAccountEventStore.cs`:

```csharp
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using Marten;

namespace FairBank.Accounts.Infrastructure.Persistence;

public sealed class MartenAccountEventStore(IDocumentSession session) : IAccountEventStore
{
    public async Task<Account?> LoadAsync(Guid accountId, CancellationToken ct = default)
    {
        return await session.Events.AggregateStreamAsync<Account>(accountId, token: ct);
    }

    public async Task StartStreamAsync(Account account, CancellationToken ct = default)
    {
        var events = account.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.StartStream<Account>(account.Id, events.ToArray());
        account.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }

    public async Task AppendEventsAsync(Account account, CancellationToken ct = default)
    {
        var events = account.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.Append(account.Id, events.ToArray());
        account.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }
}
```

**Step 3: Update `CreateAccountCommandHandler` to use `StartStreamAsync`**

In `src/Services/Accounts/FairBank.Accounts.Application/Commands/CreateAccount/CreateAccountCommandHandler.cs`, change line 15 from:
```csharp
        await eventStore.AppendEventsAsync(account, ct);
```
to:
```csharp
        await eventStore.StartStreamAsync(account, ct);
```

`DepositMoneyCommandHandler` and `WithdrawMoneyCommandHandler` already correctly call `AppendEventsAsync` — no change needed.

**Step 4: Run tests**

Run: `dotnet test FairBank.slnx`
Expected: All 40 tests pass (NSubstitute mocks — interface change requires updating mock setups if any tests call `StartStreamAsync` directly, but existing tests mock `AppendEventsAsync` which still exists)

Note: `CreateAccountCommandHandlerTests` mocks `AppendEventsAsync`. Update the mock to use `StartStreamAsync` instead:

In `tests/FairBank.Accounts.UnitTests/Application/CreateAccountCommandHandlerTests.cs`, if the test verifies `AppendEventsAsync` was called, change it to verify `StartStreamAsync` was called.

**Step 5: Commit**

```bash
git add -A && git commit -m "fix: separate StartStream and AppendToStream in Marten event store"
```

---

### Task 2: Add BCrypt NuGet Package

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/Services/Identity/FairBank.Identity.Application/FairBank.Identity.Application.csproj`

**Step 1: Add BCrypt to central package management**

In `Directory.Packages.props`, add after the `<!-- Database - Marten -->` section:

```xml
    <!-- Security -->
    <PackageVersion Include="BCrypt.Net-Next" Version="4.0.3" />
```

**Step 2: Add BCrypt reference to Identity Application csproj**

In `src/Services/Identity/FairBank.Identity.Application/FairBank.Identity.Application.csproj`, add to `<ItemGroup>`:

```xml
    <PackageReference Include="BCrypt.Net-Next" />
```

**Step 3: Update `RegisterUserCommandHandler` to use BCrypt**

In `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandHandler.cs`, replace lines 22-25:

```csharp
        // NOTE: In production, hash with BCrypt/Argon2. Simplified for now.
        var passwordHash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(request.Password)));
```

with:

```csharp
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
```

**Step 4: Build + test**

Run: `dotnet build FairBank.slnx && dotnet test FairBank.slnx`
Expected: Build succeeded, all tests pass

**Step 5: Commit**

```bash
git add -A && git commit -m "chore: add BCrypt.Net-Next and replace SHA256 password hashing"
```

---

### Task 3: PostgreSQL Primary + Replica Docker Setup

**Files:**
- Create: `docker/postgres/primary-init.sh`
- Create: `docker/postgres/replica-entrypoint.sh`
- Modify: `docker-compose.yml`

**Step 1: Create `primary-init.sh`**

Create `docker/postgres/primary-init.sh`:

```bash
#!/bin/bash
set -e

# Create replication user
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'replicator') THEN
            CREATE ROLE replicator WITH REPLICATION LOGIN PASSWORD 'replicator_2026';
        END IF;
    END
    \$\$;
EOSQL

# Allow replication connections
echo "host replication replicator all md5" >> "$PGDATA/pg_hba.conf"

# Reload config
pg_ctl reload -D "$PGDATA"
```

**Step 2: Create `replica-entrypoint.sh`**

Create `docker/postgres/replica-entrypoint.sh`:

```bash
#!/bin/bash
set -e

# Wait for primary to be ready
until pg_isready -h postgres-primary -p 5432 -U fairbank_admin; do
  echo "Waiting for primary..."
  sleep 2
done

# If data directory is empty, do base backup from primary
if [ -z "$(ls -A /var/lib/postgresql/data 2>/dev/null)" ]; then
  echo "Performing base backup from primary..."
  PGPASSWORD=replicator_2026 pg_basebackup \
    -h postgres-primary \
    -p 5432 \
    -U replicator \
    -D /var/lib/postgresql/data \
    -Fp -Xs -R -P

  touch /var/lib/postgresql/data/standby.signal

  echo "Base backup complete. Starting replica..."
fi

# Start PostgreSQL
exec postgres \
  -c hot_standby=on \
  -c shared_buffers=64MB
```

**Step 3: Make scripts executable**

Run: `chmod +x docker/postgres/primary-init.sh docker/postgres/replica-entrypoint.sh`

**Step 4: Update `docker-compose.yml`**

Replace the entire file with:

```yaml
services:

  # ─── Web App (Blazor WASM) — ONLY EXPOSED SERVICE ────────
  web-app:
    build:
      context: .
      dockerfile: src/FairBank.Web/Dockerfile
    container_name: fairbank-web
    ports:
      - "80:80"
    depends_on:
      - api-gateway
    networks:
      - backend

  # ─── Internal services (closed Docker network) ───────────
  api-gateway:
    build:
      context: .
      dockerfile: src/FairBank.ApiGateway/Dockerfile
    container_name: fairbank-api-gateway
    expose:
      - "8080"
    depends_on:
      - identity-api
      - accounts-api
    networks:
      - backend

  identity-api:
    build:
      context: .
      dockerfile: src/Services/Identity/FairBank.Identity.Api/Dockerfile
    container_name: fairbank-identity-api
    expose:
      - "8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=postgres-primary;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=identity_service"
    depends_on:
      postgres-primary:
        condition: service_healthy
    networks:
      - backend

  accounts-api:
    build:
      context: .
      dockerfile: src/Services/Accounts/FairBank.Accounts.Api/Dockerfile
    container_name: fairbank-accounts-api
    expose:
      - "8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=postgres-primary;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=accounts_service"
    depends_on:
      postgres-primary:
        condition: service_healthy
    networks:
      - backend

  # ─── PostgreSQL Primary ──────────────────────────────────
  postgres-primary:
    image: postgres:16-alpine
    container_name: fairbank-pg-primary
    environment:
      POSTGRES_DB: fairbank
      POSTGRES_USER: fairbank_admin
      POSTGRES_PASSWORD: fairbank_secret_2026
      POSTGRES_INITDB_ARGS: "--data-checksums"
    expose:
      - "5432"
    volumes:
      - pgdata-primary:/var/lib/postgresql/data
      - ./docker/postgres/primary-init.sh:/docker-entrypoint-initdb.d/00-primary.sh:ro
      - ./docker/postgres/init.sql:/docker-entrypoint-initdb.d/01-init.sql:ro
    command: >
      postgres
        -c wal_level=replica
        -c max_wal_senders=3
        -c wal_keep_size=64MB
        -c hot_standby=on
        -c shared_buffers=128MB
        -c effective_cache_size=384MB
        -c work_mem=8MB
        -c log_statement=all
        -c log_connections=on
        -c log_disconnections=on
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U fairbank_admin -d fairbank"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - backend

  # ─── PostgreSQL Replica ──────────────────────────────────
  postgres-replica:
    image: postgres:16-alpine
    container_name: fairbank-pg-replica
    environment:
      POSTGRES_USER: fairbank_admin
      POSTGRES_PASSWORD: fairbank_secret_2026
    expose:
      - "5432"
    volumes:
      - pgdata-replica:/var/lib/postgresql/data
      - ./docker/postgres/replica-entrypoint.sh:/entrypoint.sh:ro
    entrypoint: /entrypoint.sh
    depends_on:
      postgres-primary:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U fairbank_admin -d fairbank"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - backend

networks:
  backend:
    driver: bridge
    internal: false

volumes:
  pgdata-primary:
  pgdata-replica:
```

**Step 5: Validate docker-compose**

Run: `docker compose config --quiet`
Expected: No errors

**Step 6: Commit**

```bash
git add -A && git commit -m "feat(infra): add PostgreSQL primary-replica streaming replication"
```

---

### Task 4: Child Accounts — User Entity + ParentId + Migration

**Files:**
- Modify: `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs` — add ParentId, Children, CreateChild factory
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs` — add ParentId FK
- Modify: `src/Services/Identity/FairBank.Identity.Domain/Ports/IUserRepository.cs` — add GetChildrenAsync
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs` — implement GetChildrenAsync
- Test: `tests/FairBank.Identity.UnitTests/Domain/UserTests.cs` — add child creation tests

**Step 1: Add ParentId, Children, and CreateChild to User entity**

In `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs`, add after `DeletedAt` (line 18):

```csharp
    public Guid? ParentId { get; private set; }
    public User? Parent { get; private set; }
    private readonly List<User> _children = [];
    public IReadOnlyCollection<User> Children => _children.AsReadOnly();
```

Add factory method after the `Create` method (after line 46):

```csharp
    public static User CreateChild(
        string firstName,
        string lastName,
        Email email,
        string passwordHash,
        Guid parentId)
    {
        var child = Create(firstName, lastName, email, passwordHash, Enums.UserRole.Child);
        child.ParentId = parentId;
        return child;
    }
```

**Step 2: Update `UserConfiguration` for ParentId FK**

In `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs`, add before the query filter (before line 47):

```csharp
        // Parent-child self-reference
        builder.Property(u => u.ParentId);

        builder.HasOne(u => u.Parent)
            .WithMany(u => u.Children)
            .HasForeignKey(u => u.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(u => u.ParentId);

        builder.Navigation(u => u.Children).HasField("_children");
```

**Step 3: Add `GetChildrenAsync` to `IUserRepository`**

In `src/Services/Identity/FairBank.Identity.Domain/Ports/IUserRepository.cs`, add:

```csharp
    Task<IReadOnlyList<User>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);
```

**Step 4: Implement `GetChildrenAsync` in `UserRepository`**

In `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs`, add:

```csharp
    public async Task<IReadOnlyList<User>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
    {
        return await db.Users.Where(u => u.ParentId == parentId).ToListAsync(ct);
    }
```

**Step 5: Write unit tests for child creation**

Add to `tests/FairBank.Identity.UnitTests/Domain/UserTests.cs`:

```csharp
    [Fact]
    public void CreateChild_ShouldSetParentIdAndChildRole()
    {
        var parentId = Guid.NewGuid();
        var child = User.CreateChild(
            "Petr", "Novák",
            Email.Create("petr@example.com"),
            "hashedpw",
            parentId);

        child.ParentId.Should().Be(parentId);
        child.Role.Should().Be(UserRole.Child);
        child.FirstName.Should().Be("Petr");
        child.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CreateChild_WithEmptyName_ShouldThrow()
    {
        var act = () => User.CreateChild(
            "", "Novák",
            Email.Create("petr@example.com"),
            "hashedpw",
            Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }
```

**Step 6: Run tests**

Run: `dotnet test FairBank.slnx`
Expected: All tests pass (40 existing + 2 new = 42)

**Step 7: Generate EF Core migration**

Run:
```bash
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add AddParentChildRelationship \
  --project src/Services/Identity/FairBank.Identity.Infrastructure \
  --startup-project src/Services/Identity/FairBank.Identity.Api
```

Expected: Migration file created in `Persistence/Migrations/`

**Step 8: Commit**

```bash
git add -A && git commit -m "feat(identity): add ParentId self-reference FK on User for child accounts"
```

---

### Task 5: Child Accounts — CreateChild + GetChildren Commands + Endpoints

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/CreateChild/CreateChildCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/CreateChild/CreateChildCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/CreateChild/CreateChildCommandValidator.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetChildren/GetChildrenQuery.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetChildren/GetChildrenQueryHandler.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`
- Test: `tests/FairBank.Identity.UnitTests/Application/CreateChildCommandHandlerTests.cs`

**Step 1: Create `CreateChildCommand`**

Create `src/Services/Identity/FairBank.Identity.Application/Users/Commands/CreateChild/CreateChildCommand.cs`:

```csharp
using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.CreateChild;

public sealed record CreateChildCommand(
    Guid ParentId,
    string FirstName,
    string LastName,
    string Email,
    string Password) : IRequest<UserResponse>;
```

**Step 2: Create validator**

Create `src/Services/Identity/FairBank.Identity.Application/Users/Commands/CreateChild/CreateChildCommandValidator.cs`:

```csharp
using FluentValidation;

namespace FairBank.Identity.Application.Users.Commands.CreateChild;

public sealed class CreateChildCommandValidator : AbstractValidator<CreateChildCommand>
{
    public CreateChildCommandValidator()
    {
        RuleFor(x => x.ParentId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
```

**Step 3: Create handler**

Create `src/Services/Identity/FairBank.Identity.Application/Users/Commands/CreateChild/CreateChildCommandHandler.cs`:

```csharp
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.CreateChild;

public sealed class CreateChildCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateChildCommand, UserResponse>
{
    public async Task<UserResponse> Handle(CreateChildCommand request, CancellationToken ct)
    {
        // Verify parent exists and is a Client
        var parent = await userRepository.GetByIdAsync(request.ParentId, ct)
            ?? throw new InvalidOperationException("Parent user not found.");

        if (parent.Role != UserRole.Client)
            throw new InvalidOperationException("Only clients can create child accounts.");

        var email = Email.Create(request.Email);

        if (await userRepository.ExistsWithEmailAsync(email, ct))
            throw new InvalidOperationException($"User with email '{request.Email}' already exists.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        var child = User.CreateChild(
            request.FirstName,
            request.LastName,
            email,
            passwordHash,
            request.ParentId);

        await userRepository.AddAsync(child, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new UserResponse(
            child.Id,
            child.FirstName,
            child.LastName,
            child.Email.Value,
            child.Role,
            child.IsActive,
            child.CreatedAt);
    }
}
```

**Step 4: Create `GetChildrenQuery` + handler**

Create `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetChildren/GetChildrenQuery.cs`:

```csharp
using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetChildren;

public sealed record GetChildrenQuery(Guid ParentId) : IRequest<IReadOnlyList<UserResponse>>;
```

Create `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetChildren/GetChildrenQueryHandler.cs`:

```csharp
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetChildren;

public sealed class GetChildrenQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetChildrenQuery, IReadOnlyList<UserResponse>>
{
    public async Task<IReadOnlyList<UserResponse>> Handle(GetChildrenQuery request, CancellationToken ct)
    {
        var children = await userRepository.GetChildrenAsync(request.ParentId, ct);

        return children.Select(c => new UserResponse(
            c.Id,
            c.FirstName,
            c.LastName,
            c.Email.Value,
            c.Role,
            c.IsActive,
            c.CreatedAt)).ToList();
    }
}
```

**Step 5: Add endpoints to UserEndpoints**

Replace `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`:

```csharp
using FairBank.Identity.Application.Users.Commands.CreateChild;
using FairBank.Identity.Application.Users.Commands.RegisterUser;
using FairBank.Identity.Application.Users.Queries.GetChildren;
using FairBank.Identity.Application.Users.Queries.GetUserById;
using MediatR;

namespace FairBank.Identity.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("Users");

        group.MapPost("/register", async (RegisterUserCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/users/{result.Id}", result);
        })
        .WithName("RegisterUser")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetUserByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetUserById")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // Child accounts
        group.MapPost("/{parentId:guid}/children", async (Guid parentId, CreateChildCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { ParentId = parentId });
            return Results.Created($"/api/v1/users/{result.Id}", result);
        })
        .WithName("CreateChild")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{parentId:guid}/children", async (Guid parentId, ISender sender) =>
        {
            var result = await sender.Send(new GetChildrenQuery(parentId));
            return Results.Ok(result);
        })
        .WithName("GetChildren")
        .Produces(StatusCodes.Status200OK);

        return group;
    }
}
```

**Step 6: Write tests**

Create `tests/FairBank.Identity.UnitTests/Application/CreateChildCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using FairBank.Identity.Application.Users.Commands.CreateChild;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using NSubstitute;

namespace FairBank.Identity.UnitTests.Application;

public class CreateChildCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidParent_ShouldCreateChildUser()
    {
        var repo = Substitute.For<IUserRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var parentId = Guid.NewGuid();
        var parent = User.Create("Jan", "Novák", Email.Create("jan@example.com"), "hash", UserRole.Client);

        repo.GetByIdAsync(parentId, Arg.Any<CancellationToken>()).Returns(parent);
        repo.ExistsWithEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);

        var handler = new CreateChildCommandHandler(repo, uow);
        var command = new CreateChildCommand(parentId, "Petr", "Novák", "petr@example.com", "Password1!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.FirstName.Should().Be("Petr");
        result.Role.Should().Be(UserRole.Child);
        await repo.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonClientParent_ShouldThrow()
    {
        var repo = Substitute.For<IUserRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var parentId = Guid.NewGuid();
        var banker = User.Create("Bankéř", "Test", Email.Create("banker@example.com"), "hash", UserRole.Banker);

        repo.GetByIdAsync(parentId, Arg.Any<CancellationToken>()).Returns(banker);

        var handler = new CreateChildCommandHandler(repo, uow);
        var command = new CreateChildCommand(parentId, "Dítě", "Test", "child@example.com", "Password1!");

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only clients can create child accounts.");
    }
}
```

**Step 7: Run tests**

Run: `dotnet test FairBank.slnx`
Expected: 44 tests pass (42 + 2 new)

**Step 8: Commit**

```bash
git add -A && git commit -m "feat(identity): add child account creation and listing endpoints"
```

---

### Task 6: Accounts — Spending Limits on Account Aggregate

**Files:**
- Modify: `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/Account.cs` — add SpendingLimit, RequiresApproval, NeedsApproval
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/SpendingLimitSet.cs`
- Test: `tests/FairBank.Accounts.UnitTests/Domain/AccountTests.cs` — add spending limit tests

**Step 1: Create `SpendingLimitSet` event**

Create `src/Services/Accounts/FairBank.Accounts.Domain/Events/SpendingLimitSet.cs`:

```csharp
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record SpendingLimitSet(
    Guid AccountId,
    decimal Limit,
    Currency Currency,
    DateTime OccurredAt);
```

**Step 2: Add spending limit fields and methods to Account**

In `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/Account.cs`, add after `CreatedAt` (line 14):

```csharp
    public Money? SpendingLimit { get; private set; }
    public bool RequiresApproval { get; private set; }
    public Money? ApprovalThreshold { get; private set; }
```

Add method after `Deactivate()`:

```csharp
    public void SetSpendingLimit(Money limit, Money? approvalThreshold = null)
    {
        EnsureActive();
        SpendingLimit = limit;
        RequiresApproval = true;
        ApprovalThreshold = approvalThreshold ?? limit;

        RaiseEvent(new SpendingLimitSet(Id, limit.Amount, limit.Currency, DateTime.UtcNow));
    }

    public bool NeedsApproval(Money amount)
    {
        if (!RequiresApproval || ApprovalThreshold is null) return false;
        return amount.Amount > ApprovalThreshold.Amount;
    }
```

Add Apply method after existing Apply methods:

```csharp
    public void Apply(SpendingLimitSet @event)
    {
        SpendingLimit = Money.Create(@event.Limit, @event.Currency);
        RequiresApproval = true;
        ApprovalThreshold = SpendingLimit;
    }
```

Add missing using at top: `using FairBank.Accounts.Domain.Events;` (already exists for AccountCreated etc.)

**Step 3: Write tests**

Add to `tests/FairBank.Accounts.UnitTests/Domain/AccountTests.cs`:

```csharp
    [Fact]
    public void SetSpendingLimit_ShouldSetLimitAndRequireApproval()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.ClearUncommittedEvents();

        account.SetSpendingLimit(Money.Create(500, Currency.CZK));

        account.SpendingLimit!.Amount.Should().Be(500);
        account.RequiresApproval.Should().BeTrue();
        account.ApprovalThreshold!.Amount.Should().Be(500);
        account.GetUncommittedEvents().Should().HaveCount(1);
    }

    [Fact]
    public void NeedsApproval_OverThreshold_ShouldReturnTrue()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.SetSpendingLimit(Money.Create(500, Currency.CZK));

        account.NeedsApproval(Money.Create(600, Currency.CZK)).Should().BeTrue();
        account.NeedsApproval(Money.Create(400, Currency.CZK)).Should().BeFalse();
    }
```

**Step 4: Run tests**

Run: `dotnet test FairBank.slnx`
Expected: All tests pass (44 + 2 = 46)

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(accounts): add spending limits and approval threshold to Account aggregate"
```

---

### Task 7: PendingTransaction Aggregate + Commands + Endpoints

**Files:**
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/PendingTransaction.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Enums/PendingTransactionStatus.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/TransactionRequested.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/TransactionApproved.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/TransactionRejected.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Ports/IPendingTransactionStore.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/DTOs/PendingTransactionResponse.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/SetSpendingLimit/SetSpendingLimitCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/SetSpendingLimit/SetSpendingLimitCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/ApproveTransaction/ApproveTransactionCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/ApproveTransaction/ApproveTransactionCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/RejectTransaction/RejectTransactionCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/RejectTransaction/RejectTransactionCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetPendingTransactions/GetPendingTransactionsQuery.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetPendingTransactions/GetPendingTransactionsQueryHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/MartenPendingTransactionStore.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Infrastructure/DependencyInjection.cs` — register PendingTransaction projection + store
- Modify: `src/Services/Accounts/FairBank.Accounts.Api/Endpoints/AccountEndpoints.cs` — add new endpoints
- Test: `tests/FairBank.Accounts.UnitTests/Domain/PendingTransactionTests.cs`

**Step 1: Create domain events**

Create `src/Services/Accounts/FairBank.Accounts.Domain/Events/TransactionRequested.cs`:

```csharp
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record TransactionRequested(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description,
    Guid RequestedBy,
    DateTime OccurredAt);
```

Create `src/Services/Accounts/FairBank.Accounts.Domain/Events/TransactionApproved.cs`:

```csharp
namespace FairBank.Accounts.Domain.Events;

public sealed record TransactionApproved(
    Guid TransactionId,
    Guid ApproverId,
    DateTime OccurredAt);
```

Create `src/Services/Accounts/FairBank.Accounts.Domain/Events/TransactionRejected.cs`:

```csharp
namespace FairBank.Accounts.Domain.Events;

public sealed record TransactionRejected(
    Guid TransactionId,
    Guid ApproverId,
    string Reason,
    DateTime OccurredAt);
```

**Step 2: Create `PendingTransactionStatus` enum**

Create `src/Services/Accounts/FairBank.Accounts.Domain/Enums/PendingTransactionStatus.cs`:

```csharp
namespace FairBank.Accounts.Domain.Enums;

public enum PendingTransactionStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
```

**Step 3: Create `PendingTransaction` aggregate**

Create `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/PendingTransaction.cs`:

```csharp
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.Domain.Aggregates;

public sealed class PendingTransaction
{
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public Guid RequestedBy { get; private set; }
    public Guid? ApproverId { get; private set; }
    public PendingTransactionStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    private readonly List<object> _uncommittedEvents = [];

    private PendingTransaction() { }

    public static PendingTransaction Create(
        Guid accountId,
        Money amount,
        string description,
        Guid requestedBy)
    {
        var tx = new PendingTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = amount,
            Description = description,
            RequestedBy = requestedBy,
            Status = PendingTransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        tx.RaiseEvent(new TransactionRequested(
            tx.Id, accountId, amount.Amount, amount.Currency, description, requestedBy, DateTime.UtcNow));

        return tx;
    }

    public void Approve(Guid approverId)
    {
        if (Status != PendingTransactionStatus.Pending)
            throw new InvalidOperationException("Transaction is not pending.");

        Status = PendingTransactionStatus.Approved;
        ApproverId = approverId;
        ResolvedAt = DateTime.UtcNow;

        RaiseEvent(new TransactionApproved(Id, approverId, DateTime.UtcNow));
    }

    public void Reject(Guid approverId, string reason)
    {
        if (Status != PendingTransactionStatus.Pending)
            throw new InvalidOperationException("Transaction is not pending.");

        Status = PendingTransactionStatus.Rejected;
        ApproverId = approverId;
        RejectionReason = reason;
        ResolvedAt = DateTime.UtcNow;

        RaiseEvent(new TransactionRejected(Id, approverId, reason, DateTime.UtcNow));
    }

    public IReadOnlyList<object> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();
    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();
    private void RaiseEvent(object @event) => _uncommittedEvents.Add(@event);

    // Marten Apply methods
    public void Apply(TransactionRequested @event)
    {
        Id = @event.TransactionId;
        AccountId = @event.AccountId;
        Amount = Money.Create(@event.Amount, @event.Currency);
        Description = @event.Description;
        RequestedBy = @event.RequestedBy;
        Status = PendingTransactionStatus.Pending;
        CreatedAt = @event.OccurredAt;
    }

    public void Apply(TransactionApproved @event)
    {
        Status = PendingTransactionStatus.Approved;
        ApproverId = @event.ApproverId;
        ResolvedAt = @event.OccurredAt;
    }

    public void Apply(TransactionRejected @event)
    {
        Status = PendingTransactionStatus.Rejected;
        ApproverId = @event.ApproverId;
        RejectionReason = @event.Reason;
        ResolvedAt = @event.OccurredAt;
    }
}
```

**Step 4: Create `IPendingTransactionStore`**

Create `src/Services/Accounts/FairBank.Accounts.Application/Ports/IPendingTransactionStore.cs`:

```csharp
using FairBank.Accounts.Domain.Aggregates;

namespace FairBank.Accounts.Application.Ports;

public interface IPendingTransactionStore
{
    Task<PendingTransaction?> LoadAsync(Guid transactionId, CancellationToken ct = default);
    Task<IReadOnlyList<PendingTransaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    Task StartStreamAsync(PendingTransaction transaction, CancellationToken ct = default);
    Task AppendEventsAsync(PendingTransaction transaction, CancellationToken ct = default);
}
```

**Step 5: Create `PendingTransactionResponse` DTO**

Create `src/Services/Accounts/FairBank.Accounts.Application/DTOs/PendingTransactionResponse.cs`:

```csharp
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Application.DTOs;

public sealed record PendingTransactionResponse(
    Guid Id,
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description,
    Guid RequestedBy,
    PendingTransactionStatus Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt);
```

**Step 6: Create commands and queries**

Create `src/Services/Accounts/FairBank.Accounts.Application/Commands/SetSpendingLimit/SetSpendingLimitCommand.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.SetSpendingLimit;

public sealed record SetSpendingLimitCommand(
    Guid AccountId,
    decimal Limit,
    Currency Currency) : IRequest<AccountResponse>;
```

Create `src/Services/Accounts/FairBank.Accounts.Application/Commands/SetSpendingLimit/SetSpendingLimitCommandHandler.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.SetSpendingLimit;

public sealed class SetSpendingLimitCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<SetSpendingLimitCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(SetSpendingLimitCommand request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException("Account not found.");

        account.SetSpendingLimit(Money.Create(request.Limit, request.Currency));
        await eventStore.AppendEventsAsync(account, ct);

        return new AccountResponse(
            account.Id, account.OwnerId, account.AccountNumber.Value,
            account.Balance.Amount, account.Balance.Currency,
            account.IsActive, account.CreatedAt);
    }
}
```

Create `src/Services/Accounts/FairBank.Accounts.Application/Commands/ApproveTransaction/ApproveTransactionCommand.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Commands.ApproveTransaction;

public sealed record ApproveTransactionCommand(
    Guid TransactionId,
    Guid ApproverId) : IRequest<PendingTransactionResponse>;
```

Create `src/Services/Accounts/FairBank.Accounts.Application/Commands/ApproveTransaction/ApproveTransactionCommandHandler.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.ApproveTransaction;

public sealed class ApproveTransactionCommandHandler(
    IPendingTransactionStore pendingStore,
    IAccountEventStore accountStore)
    : IRequestHandler<ApproveTransactionCommand, PendingTransactionResponse>
{
    public async Task<PendingTransactionResponse> Handle(ApproveTransactionCommand request, CancellationToken ct)
    {
        var tx = await pendingStore.LoadAsync(request.TransactionId, ct)
            ?? throw new InvalidOperationException("Pending transaction not found.");

        tx.Approve(request.ApproverId);
        await pendingStore.AppendEventsAsync(tx, ct);

        // Execute the actual withdrawal
        var account = await accountStore.LoadAsync(tx.AccountId, ct)
            ?? throw new InvalidOperationException("Account not found.");

        account.Withdraw(tx.Amount, tx.Description);
        await accountStore.AppendEventsAsync(account, ct);

        return new PendingTransactionResponse(
            tx.Id, tx.AccountId, tx.Amount.Amount, tx.Amount.Currency,
            tx.Description, tx.RequestedBy, tx.Status, tx.CreatedAt, tx.ResolvedAt);
    }
}
```

Create `src/Services/Accounts/FairBank.Accounts.Application/Commands/RejectTransaction/RejectTransactionCommand.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Commands.RejectTransaction;

public sealed record RejectTransactionCommand(
    Guid TransactionId,
    Guid ApproverId,
    string Reason) : IRequest<PendingTransactionResponse>;
```

Create `src/Services/Accounts/FairBank.Accounts.Application/Commands/RejectTransaction/RejectTransactionCommandHandler.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.RejectTransaction;

public sealed class RejectTransactionCommandHandler(IPendingTransactionStore pendingStore)
    : IRequestHandler<RejectTransactionCommand, PendingTransactionResponse>
{
    public async Task<PendingTransactionResponse> Handle(RejectTransactionCommand request, CancellationToken ct)
    {
        var tx = await pendingStore.LoadAsync(request.TransactionId, ct)
            ?? throw new InvalidOperationException("Pending transaction not found.");

        tx.Reject(request.ApproverId, request.Reason);
        await pendingStore.AppendEventsAsync(tx, ct);

        return new PendingTransactionResponse(
            tx.Id, tx.AccountId, tx.Amount.Amount, tx.Amount.Currency,
            tx.Description, tx.RequestedBy, tx.Status, tx.CreatedAt, tx.ResolvedAt);
    }
}
```

Create `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetPendingTransactions/GetPendingTransactionsQuery.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetPendingTransactions;

public sealed record GetPendingTransactionsQuery(Guid AccountId) : IRequest<IReadOnlyList<PendingTransactionResponse>>;
```

Create `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetPendingTransactions/GetPendingTransactionsQueryHandler.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetPendingTransactions;

public sealed class GetPendingTransactionsQueryHandler(IPendingTransactionStore store)
    : IRequestHandler<GetPendingTransactionsQuery, IReadOnlyList<PendingTransactionResponse>>
{
    public async Task<IReadOnlyList<PendingTransactionResponse>> Handle(GetPendingTransactionsQuery request, CancellationToken ct)
    {
        var txs = await store.GetByAccountIdAsync(request.AccountId, ct);
        return txs.Select(t => new PendingTransactionResponse(
            t.Id, t.AccountId, t.Amount.Amount, t.Amount.Currency,
            t.Description, t.RequestedBy, t.Status, t.CreatedAt, t.ResolvedAt)).ToList();
    }
}
```

**Step 7: Create `MartenPendingTransactionStore`**

Create `src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/MartenPendingTransactionStore.cs`:

```csharp
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using Marten;

namespace FairBank.Accounts.Infrastructure.Persistence;

public sealed class MartenPendingTransactionStore(IDocumentSession session) : IPendingTransactionStore
{
    public async Task<PendingTransaction?> LoadAsync(Guid transactionId, CancellationToken ct = default)
    {
        return await session.Events.AggregateStreamAsync<PendingTransaction>(transactionId, token: ct);
    }

    public async Task<IReadOnlyList<PendingTransaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        return await session.Query<PendingTransaction>()
            .Where(t => t.AccountId == accountId && t.Status == PendingTransactionStatus.Pending)
            .ToListAsync(ct);
    }

    public async Task StartStreamAsync(PendingTransaction transaction, CancellationToken ct = default)
    {
        var events = transaction.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.StartStream<PendingTransaction>(transaction.Id, events.ToArray());
        transaction.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }

    public async Task AppendEventsAsync(PendingTransaction transaction, CancellationToken ct = default)
    {
        var events = transaction.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.Append(transaction.Id, events.ToArray());
        transaction.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }
}
```

**Step 8: Register in DI and add Marten projections**

Replace `src/Services/Accounts/FairBank.Accounts.Infrastructure/DependencyInjection.cs`:

```csharp
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
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
        string connectionString)
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
            options.Projections.Snapshot<PendingTransaction>(SnapshotLifecycle.Inline);
        })
        .UseLightweightSessions();

        services.AddScoped<IAccountEventStore, MartenAccountEventStore>();
        services.AddScoped<IPendingTransactionStore, MartenPendingTransactionStore>();

        return services;
    }
}
```

**Step 9: Add endpoints**

Replace `src/Services/Accounts/FairBank.Accounts.Api/Endpoints/AccountEndpoints.cs`:

```csharp
using FairBank.Accounts.Application.Commands.ApproveTransaction;
using FairBank.Accounts.Application.Commands.CreateAccount;
using FairBank.Accounts.Application.Commands.DepositMoney;
using FairBank.Accounts.Application.Commands.RejectTransaction;
using FairBank.Accounts.Application.Commands.SetSpendingLimit;
using FairBank.Accounts.Application.Commands.WithdrawMoney;
using FairBank.Accounts.Application.Queries.GetAccountById;
using FairBank.Accounts.Application.Queries.GetPendingTransactions;
using MediatR;

namespace FairBank.Accounts.Api.Endpoints;

public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounts")
            .WithTags("Accounts");

        group.MapPost("/", async (CreateAccountCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/accounts/{result.Id}", result);
        })
        .WithName("CreateAccount")
        .Produces(StatusCodes.Status201Created);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAccountByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetAccountById")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/deposit", async (Guid id, DepositMoneyCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("DepositMoney")
        .Produces(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/withdraw", async (Guid id, WithdrawMoneyCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("WithdrawMoney")
        .Produces(StatusCodes.Status200OK);

        // Spending limits
        group.MapPost("/{id:guid}/limits", async (Guid id, SetSpendingLimitCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("SetSpendingLimit")
        .Produces(StatusCodes.Status200OK);

        // Pending transactions
        group.MapGet("/{id:guid}/pending", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPendingTransactionsQuery(id));
            return Results.Ok(result);
        })
        .WithName("GetPendingTransactions")
        .Produces(StatusCodes.Status200OK);

        // Approve/Reject pending transactions
        var pendingGroup = app.MapGroup("/api/v1/accounts/pending")
            .WithTags("PendingTransactions");

        pendingGroup.MapPost("/{id:guid}/approve", async (Guid id, ApproveTransactionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { TransactionId = id });
            return Results.Ok(result);
        })
        .WithName("ApproveTransaction")
        .Produces(StatusCodes.Status200OK);

        pendingGroup.MapPost("/{id:guid}/reject", async (Guid id, RejectTransactionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { TransactionId = id });
            return Results.Ok(result);
        })
        .WithName("RejectTransaction")
        .Produces(StatusCodes.Status200OK);

        return group;
    }
}
```

**Step 10: Write tests for PendingTransaction**

Create `tests/FairBank.Accounts.UnitTests/Domain/PendingTransactionTests.cs`:

```csharp
using FluentAssertions;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Domain;

public class PendingTransactionTests
{
    [Fact]
    public void Create_ShouldInitializeWithPendingStatus()
    {
        var tx = PendingTransaction.Create(
            Guid.NewGuid(),
            Money.Create(100, Currency.CZK),
            "Test withdrawal",
            Guid.NewGuid());

        tx.Status.Should().Be(PendingTransactionStatus.Pending);
        tx.Amount.Amount.Should().Be(100);
        tx.GetUncommittedEvents().Should().HaveCount(1);
    }

    [Fact]
    public void Approve_ShouldChangeStatusToApproved()
    {
        var tx = PendingTransaction.Create(
            Guid.NewGuid(),
            Money.Create(100, Currency.CZK),
            "Test",
            Guid.NewGuid());
        tx.ClearUncommittedEvents();

        var approverId = Guid.NewGuid();
        tx.Approve(approverId);

        tx.Status.Should().Be(PendingTransactionStatus.Approved);
        tx.ApproverId.Should().Be(approverId);
        tx.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reject_ShouldChangeStatusToRejected()
    {
        var tx = PendingTransaction.Create(
            Guid.NewGuid(),
            Money.Create(100, Currency.CZK),
            "Test",
            Guid.NewGuid());
        tx.ClearUncommittedEvents();

        var approverId = Guid.NewGuid();
        tx.Reject(approverId, "Too expensive");

        tx.Status.Should().Be(PendingTransactionStatus.Rejected);
        tx.RejectionReason.Should().Be("Too expensive");
    }

    [Fact]
    public void Approve_AlreadyApproved_ShouldThrow()
    {
        var tx = PendingTransaction.Create(
            Guid.NewGuid(),
            Money.Create(100, Currency.CZK),
            "Test",
            Guid.NewGuid());
        tx.Approve(Guid.NewGuid());

        var act = () => tx.Approve(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }
}
```

**Step 11: Run build + tests**

Run: `dotnet build FairBank.slnx && dotnet test FairBank.slnx`
Expected: All tests pass (46 + 4 = 50)

**Step 12: Commit**

```bash
git add -A && git commit -m "feat(accounts): add PendingTransaction aggregate, spending limit commands, and approval endpoints"
```

---

### Task 8: Frontend — Extend API Client for Children & Pending Transactions

> **Already exists (DO NOT modify):** `AuthService.cs`, `IAuthService.cs`, `Login.razor`, `Register.razor`,
> `AuthGuard.razor`, `LoginRequest.cs`, `LoginResponse.cs`, `AuthSession.cs`, `RegisterRequest.cs`,
> and existing methods in `IFairBankApi.cs` / `FairBankApiClient.cs`.

**Files:**
- Create: `src/FairBank.Web.Shared/Models/PendingTransactionDto.cs`
- Modify: `src/FairBank.Web.Shared/Services/IFairBankApi.cs` — add children + pending methods
- Modify: `src/FairBank.Web.Shared/Services/FairBankApiClient.cs` — implement new methods

**Step 1: Create `PendingTransactionDto`**

Create `src/FairBank.Web.Shared/Models/PendingTransactionDto.cs`:

```csharp
namespace FairBank.Web.Shared.Models;

public sealed record PendingTransactionDto(
    Guid Id,
    Guid AccountId,
    decimal Amount,
    string Currency,
    string Description,
    Guid RequestedBy,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt);
```

**Step 2: Extend `IFairBankApi`**

In `src/FairBank.Web.Shared/Services/IFairBankApi.cs`, add after the Auth section (after line 21):

```csharp

    // Children
    Task<List<UserResponse>> GetChildrenAsync(Guid parentId);
    Task<UserResponse> CreateChildAsync(Guid parentId, string firstName, string lastName, string email, string password);

    // Account queries
    Task<List<AccountResponse>> GetAccountsByOwnerAsync(Guid ownerId);

    // Pending transactions
    Task<List<PendingTransactionDto>> GetPendingTransactionsAsync(Guid accountId);
    Task<PendingTransactionDto> ApproveTransactionAsync(Guid transactionId, Guid approverId);
    Task<PendingTransactionDto> RejectTransactionAsync(Guid transactionId, Guid approverId, string reason);
```

**Step 3: Implement in `FairBankApiClient`**

In `src/FairBank.Web.Shared/Services/FairBankApiClient.cs`, add before the closing `}` of the class:

```csharp

    // ── Children ────────────────────────────────────────────────
    public async Task<List<UserResponse>> GetChildrenAsync(Guid parentId)
    {
        return await http.GetFromJsonAsync<List<UserResponse>>($"api/v1/users/{parentId}/children") ?? [];
    }

    public async Task<UserResponse> CreateChildAsync(Guid parentId, string firstName, string lastName, string email, string password)
    {
        var response = await http.PostAsJsonAsync($"api/v1/users/{parentId}/children",
            new { FirstName = firstName, LastName = lastName, Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    // ── Account queries ─────────────────────────────────────────
    public async Task<List<AccountResponse>> GetAccountsByOwnerAsync(Guid ownerId)
    {
        return await http.GetFromJsonAsync<List<AccountResponse>>($"api/v1/accounts?ownerId={ownerId}") ?? [];
    }

    // ── Pending transactions ────────────────────────────────────
    public async Task<List<PendingTransactionDto>> GetPendingTransactionsAsync(Guid accountId)
    {
        return await http.GetFromJsonAsync<List<PendingTransactionDto>>($"api/v1/accounts/{accountId}/pending") ?? [];
    }

    public async Task<PendingTransactionDto> ApproveTransactionAsync(Guid transactionId, Guid approverId)
    {
        var response = await http.PostAsJsonAsync($"api/v1/accounts/pending/{transactionId}/approve",
            new { ApproverId = approverId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PendingTransactionDto>())!;
    }

    public async Task<PendingTransactionDto> RejectTransactionAsync(Guid transactionId, Guid approverId, string reason)
    {
        var response = await http.PostAsJsonAsync($"api/v1/accounts/pending/{transactionId}/reject",
            new { ApproverId = approverId, Reason = reason });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PendingTransactionDto>())!;
    }
```

**Step 4: Build**

Run: `dotnet build FairBank.slnx`
Expected: Build succeeded

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(frontend): extend IFairBankApi with children and pending transaction methods"
```

---

### Task 9: Final — Run All Tests + Docker Config Validation

**Step 1: Run full test suite**

Run: `dotnet test FairBank.slnx -v minimal`
Expected: All tests pass (50 tests)

**Step 2: Validate docker-compose**

Run: `docker compose config --quiet`
Expected: No errors

**Step 3: Final commit if needed**

If any fixes were required:
```bash
git add -A && git commit -m "fix: resolve build/test issues from integration"
```

**Step 4: Push**

```bash
git push
```
