# FairBank API & Database Foundation — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Set up the hexagonal microservices foundation with two services (Identity + Accounts), PostgreSQL database with separate schemas, Docker Compose orchestration, and working Minimal API endpoints.

**Architecture:** Hexagonal (Ports & Adapters) microservices. Each service has Domain (entities, ports), Application (use cases, MediatR handlers), Infrastructure (adapters: EF Core or Marten), and Api (Minimal API endpoints) layers. Services communicate via Kafka events (wired later). Shared DB with per-service schemas.

**Tech Stack:** .NET 10, C#, PostgreSQL 16, Marten (Event Sourcing), EF Core (CRUD), MediatR, FluentValidation, Docker Compose, YARP (API Gateway)

---

## Solution Structure

```
FairBank/
├── src/
│   ├── FairBank.SharedKernel/                    (.NET class library)
│   ├── FairBank.ApiGateway/                      (.NET web app - YARP)
│   ├── Services/
│   │   ├── Identity/
│   │   │   ├── FairBank.Identity.Domain/         (.NET class library)
│   │   │   ├── FairBank.Identity.Application/    (.NET class library)
│   │   │   ├── FairBank.Identity.Infrastructure/ (.NET class library)
│   │   │   └── FairBank.Identity.Api/            (.NET web app)
│   │   └── Accounts/
│   │       ├── FairBank.Accounts.Domain/         (.NET class library)
│   │       ├── FairBank.Accounts.Application/    (.NET class library)
│   │       ├── FairBank.Accounts.Infrastructure/ (.NET class library)
│   │       └── FairBank.Accounts.Api/            (.NET web app)
├── tests/
│   ├── FairBank.Identity.UnitTests/              (.NET xUnit)
│   ├── FairBank.Identity.IntegrationTests/       (.NET xUnit)
│   ├── FairBank.Accounts.UnitTests/              (.NET xUnit)
│   └── FairBank.Accounts.IntegrationTests/       (.NET xUnit)
├── docker-compose.yml
├── docker-compose.override.yml
├── .gitignore
└── FairBank.sln
```

## Dependency Rules (Hexagonal Architecture)

```
Domain ← Application ← Infrastructure
                     ← Api

Domain:         ZERO dependencies (only SharedKernel)
Application:    depends on Domain (references ports/interfaces)
Infrastructure: depends on Application + Domain (implements ports)
Api:            depends on Application + Infrastructure (DI wiring)

SharedKernel:   referenced by all Domain projects
```

---

## Task 1: Solution Scaffolding + .gitignore

**Files:**
- Create: `FairBank.sln`
- Create: `.gitignore`
- Create: `Directory.Build.props` (shared build properties)
- Create: `Directory.Packages.props` (central package management)

**Step 1: Create .gitignore**

```bash
cd /home/kamil/Job/fai
dotnet new gitignore
```

Append Docker and IDE entries:

```gitignore
# Docker
docker-compose.override.yml

# Secrets
**/appsettings.*.json
!**/appsettings.json
!**/appsettings.Development.json

# IDE
.idea/
*.suo
*.user
```

**Step 2: Create solution and Directory.Build.props**

```bash
dotnet new sln -n FairBank
```

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Create `Directory.Packages.props` (Central Package Management):

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- CQRS & Validation -->
    <PackageVersion Include="MediatR" Version="12.*" />
    <PackageVersion Include="FluentValidation" Version="11.*" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />

    <!-- Database - EF Core -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.*" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.*" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.*" />

    <!-- Database - Marten (Event Sourcing) -->
    <PackageVersion Include="Marten" Version="7.*" />

    <!-- API Gateway -->
    <PackageVersion Include="Yarp.ReverseProxy" Version="2.*" />

    <!-- Logging -->
    <PackageVersion Include="Serilog.AspNetCore" Version="9.*" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="6.*" />

    <!-- Testing -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageVersion Include="xunit" Version="2.*" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageVersion Include="FluentAssertions" Version="7.*" />
    <PackageVersion Include="NSubstitute" Version="5.*" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.*" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
  </ItemGroup>
</Project>
```

**Step 3: Commit**

```bash
git add .gitignore FairBank.sln Directory.Build.props Directory.Packages.props
git commit -m "chore: initialize solution with central package management"
```

---

## Task 2: SharedKernel Project

**Files:**
- Create: `src/FairBank.SharedKernel/FairBank.SharedKernel.csproj`
- Create: `src/FairBank.SharedKernel/Domain/Entity.cs`
- Create: `src/FairBank.SharedKernel/Domain/AggregateRoot.cs`
- Create: `src/FairBank.SharedKernel/Domain/ValueObject.cs`
- Create: `src/FairBank.SharedKernel/Domain/IDomainEvent.cs`
- Create: `src/FairBank.SharedKernel/Domain/IRepository.cs`
- Create: `src/FairBank.SharedKernel/Application/IUnitOfWork.cs`

**Step 1: Create project and add to solution**

```bash
mkdir -p src/FairBank.SharedKernel
cd /home/kamil/Job/fai
dotnet new classlib -n FairBank.SharedKernel -o src/FairBank.SharedKernel
dotnet sln add src/FairBank.SharedKernel/FairBank.SharedKernel.csproj
```

Remove auto-generated `Class1.cs`.

**Step 2: Add MediatR dependency (for IDomainEvent)**

`src/FairBank.SharedKernel/FairBank.SharedKernel.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="MediatR" />
  </ItemGroup>
</Project>
```

**Step 3: Create base domain classes**

`src/FairBank.SharedKernel/Domain/Entity.cs`:

```csharp
namespace FairBank.SharedKernel.Domain;

public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => Id.GetHashCode();
}
```

`src/FairBank.SharedKernel/Domain/AggregateRoot.cs`:

```csharp
namespace FairBank.SharedKernel.Domain;

public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
```

`src/FairBank.SharedKernel/Domain/ValueObject.cs`:

```csharp
namespace FairBank.SharedKernel.Domain;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetAtomicValues();

    public override bool Equals(object? obj)
    {
        if (obj is not ValueObject other) return false;
        return GetAtomicValues().SequenceEqual(other.GetAtomicValues());
    }

    public override int GetHashCode()
    {
        return GetAtomicValues()
            .Aggregate(0, (hash, value) =>
                HashCode.Combine(hash, value?.GetHashCode() ?? 0));
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
```

`src/FairBank.SharedKernel/Domain/IDomainEvent.cs`:

```csharp
using MediatR;

namespace FairBank.SharedKernel.Domain;

public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

`src/FairBank.SharedKernel/Domain/IRepository.cs`:

```csharp
namespace FairBank.SharedKernel.Domain;

public interface IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task AddAsync(TAggregate aggregate, CancellationToken ct = default);
    Task UpdateAsync(TAggregate aggregate, CancellationToken ct = default);
}
```

`src/FairBank.SharedKernel/Application/IUnitOfWork.cs`:

```csharp
namespace FairBank.SharedKernel.Application;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

**Step 4: Verify build**

```bash
dotnet build src/FairBank.SharedKernel/
```
Expected: Build succeeded.

**Step 5: Commit**

```bash
git add src/FairBank.SharedKernel/ FairBank.sln
git commit -m "feat: add SharedKernel with base domain classes (Entity, AggregateRoot, ValueObject)"
```

---

## Task 3: Docker Compose — PostgreSQL

**Files:**
- Create: `docker-compose.yml`
- Create: `docker-compose.override.yml`
- Create: `docker/postgres/init.sql`

**Step 1: Create PostgreSQL init script (creates schemas)**

`docker/postgres/init.sql`:

```sql
-- Create separate schemas for each microservice
CREATE SCHEMA IF NOT EXISTS identity_service;
CREATE SCHEMA IF NOT EXISTS accounts_service;

-- Grant permissions (services connect as fairbank_app user)
GRANT ALL PRIVILEGES ON SCHEMA identity_service TO fairbank_app;
GRANT ALL PRIVILEGES ON SCHEMA accounts_service TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity_service GRANT ALL ON TABLES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA accounts_service GRANT ALL ON TABLES TO fairbank_app;
```

**Step 2: Create docker-compose.yml**

`docker-compose.yml`:

```yaml
services:
  postgres:
    image: postgres:16-alpine
    container_name: fairbank-postgres
    environment:
      POSTGRES_DB: fairbank
      POSTGRES_USER: fairbank_admin
      POSTGRES_PASSWORD: fairbank_secret_2026
      POSTGRES_INITDB_ARGS: "--data-checksums"
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./docker/postgres/init.sql:/docker-entrypoint-initdb.d/01-init.sql:ro
    command: >
      postgres
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

networks:
  backend:
    driver: bridge

volumes:
  pgdata:
```

**Step 3: Update init.sql to create the app user before granting**

`docker/postgres/init.sql` (full version):

```sql
-- Create application user (services connect as this user)
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'fairbank_app') THEN
        CREATE ROLE fairbank_app WITH LOGIN PASSWORD 'fairbank_app_2026';
    END IF;
END
$$;

-- Create separate schemas for each microservice
CREATE SCHEMA IF NOT EXISTS identity_service;
CREATE SCHEMA IF NOT EXISTS accounts_service;

-- Grant permissions
GRANT CONNECT ON DATABASE fairbank TO fairbank_app;
GRANT ALL PRIVILEGES ON SCHEMA identity_service TO fairbank_app;
GRANT ALL PRIVILEGES ON SCHEMA accounts_service TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity_service GRANT ALL ON TABLES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA accounts_service GRANT ALL ON TABLES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity_service GRANT ALL ON SEQUENCES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA accounts_service GRANT ALL ON SEQUENCES TO fairbank_app;
```

**Step 4: Start PostgreSQL and verify**

```bash
docker compose up -d postgres
docker compose exec postgres psql -U fairbank_admin -d fairbank -c "\dn"
```

Expected: Shows `identity_service` and `accounts_service` schemas.

**Step 5: Commit**

```bash
git add docker-compose.yml docker/
git commit -m "infra: add Docker Compose with PostgreSQL and per-service schemas"
```

---

## Task 4: Identity Service — Domain Layer

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Domain/FairBank.Identity.Domain.csproj`
- Create: `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/Enums/UserRole.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/ValueObjects/Email.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/ValueObjects/PasswordHash.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/Ports/IUserRepository.cs`
- Create: `tests/FairBank.Identity.UnitTests/FairBank.Identity.UnitTests.csproj`
- Create: `tests/FairBank.Identity.UnitTests/Domain/UserTests.cs`
- Create: `tests/FairBank.Identity.UnitTests/Domain/EmailTests.cs`

**Step 1: Create projects**

```bash
cd /home/kamil/Job/fai

# Domain project
dotnet new classlib -n FairBank.Identity.Domain -o src/Services/Identity/FairBank.Identity.Domain
dotnet sln add src/Services/Identity/FairBank.Identity.Domain/FairBank.Identity.Domain.csproj

# Unit test project
dotnet new xunit -n FairBank.Identity.UnitTests -o tests/FairBank.Identity.UnitTests
dotnet sln add tests/FairBank.Identity.UnitTests/FairBank.Identity.UnitTests.csproj
```

Remove auto-generated files (`Class1.cs`, `UnitTest1.cs`).

**Step 2: Set up project references**

`src/Services/Identity/FairBank.Identity.Domain/FairBank.Identity.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../../../FairBank.SharedKernel/FairBank.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

`tests/FairBank.Identity.UnitTests/FairBank.Identity.UnitTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Services/Identity/FairBank.Identity.Domain/FairBank.Identity.Domain.csproj" />
  </ItemGroup>
</Project>
```

**Step 3: Write failing tests for Email value object**

`tests/FairBank.Identity.UnitTests/Domain/EmailTests.cs`:

```csharp
using FluentAssertions;
using FairBank.Identity.Domain.ValueObjects;

namespace FairBank.Identity.UnitTests.Domain;

public class EmailTests
{
    [Fact]
    public void Create_WithValidEmail_ShouldSucceed()
    {
        var email = Email.Create("user@example.com");
        email.Value.Should().Be("user@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("@no-local-part.com")]
    [InlineData("no-at-sign")]
    public void Create_WithInvalidEmail_ShouldThrow(string invalidEmail)
    {
        var act = () => Email.Create(invalidEmail);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TwoEmails_WithSameValue_ShouldBeEqual()
    {
        var email1 = Email.Create("user@example.com");
        var email2 = Email.Create("user@example.com");
        email1.Should().Be(email2);
    }
}
```

**Step 4: Run tests — verify they fail**

```bash
dotnet test tests/FairBank.Identity.UnitTests/ --verbosity normal
```

Expected: FAIL — `Email` class does not exist yet.

**Step 5: Implement Email value object**

`src/Services/Identity/FairBank.Identity.Domain/ValueObjects/Email.cs`:

```csharp
using System.Text.RegularExpressions;
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.ValueObjects;

public sealed partial class Email : ValueObject
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));

        var trimmed = email.Trim().ToLowerInvariant();

        if (!EmailRegex().IsMatch(trimmed))
            throw new ArgumentException($"Invalid email format: {email}", nameof(email));

        return new Email(trimmed);
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
```

**Step 6: Run tests — verify they pass**

```bash
dotnet test tests/FairBank.Identity.UnitTests/ --verbosity normal
```

Expected: All 7 tests PASS.

**Step 7: Write failing tests for User entity**

`tests/FairBank.Identity.UnitTests/Domain/UserTests.cs`:

```csharp
using FluentAssertions;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.ValueObjects;

namespace FairBank.Identity.UnitTests.Domain;

public class UserTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateUser()
    {
        var user = User.Create(
            firstName: "Jan",
            lastName: "Novák",
            email: Email.Create("jan@example.com"),
            passwordHash: "hashed_password_123",
            role: UserRole.Client);

        user.Id.Should().NotBe(Guid.Empty);
        user.FirstName.Should().Be("Jan");
        user.LastName.Should().Be("Novák");
        user.Email.Value.Should().Be("jan@example.com");
        user.Role.Should().Be(UserRole.Client);
        user.IsActive.Should().BeTrue();
        user.IsDeleted.Should().BeFalse();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithEmptyFirstName_ShouldThrow()
    {
        var act = () => User.Create("", "Novák", Email.Create("jan@example.com"), "hash", UserRole.Client);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SoftDelete_ShouldMarkAsDeleted()
    {
        var user = User.Create("Jan", "Novák", Email.Create("jan@example.com"), "hash", UserRole.Client);

        user.SoftDelete();

        user.IsDeleted.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Restore_ShouldUnmarkDeleted()
    {
        var user = User.Create("Jan", "Novák", Email.Create("jan@example.com"), "hash", UserRole.Client);
        user.SoftDelete();

        user.Restore();

        user.IsDeleted.Should().BeFalse();
        user.IsActive.Should().BeTrue();
        user.DeletedAt.Should().BeNull();
    }
}
```

**Step 8: Run tests — verify they fail**

```bash
dotnet test tests/FairBank.Identity.UnitTests/ --verbosity normal
```

Expected: FAIL — `User`, `UserRole` do not exist yet.

**Step 9: Implement UserRole enum and User entity**

`src/Services/Identity/FairBank.Identity.Domain/Enums/UserRole.cs`:

```csharp
namespace FairBank.Identity.Domain.Enums;

public enum UserRole
{
    Client = 0,
    Child = 1,
    Banker = 2,
    Admin = 3
}
```

`src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs`:

```csharp
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Entities;

public sealed class User : AggregateRoot<Guid>
{
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private User() { } // EF Core

    public static User Create(
        string firstName,
        string lastName,
        Email email,
        string passwordHash,
        UserRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName, nameof(firstName));
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName, nameof(lastName));
        ArgumentNullException.ThrowIfNull(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash, nameof(passwordHash));

        return new User
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        IsActive = false;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        IsActive = true;
        DeletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

**Step 10: Create port (repository interface)**

`src/Services/Identity/FairBank.Identity.Domain/Ports/IUserRepository.cs`:

```csharp
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Ports;

public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<bool> ExistsWithEmailAsync(Email email, CancellationToken ct = default);
}
```

**Step 11: Run all tests — verify they pass**

```bash
dotnet test tests/FairBank.Identity.UnitTests/ --verbosity normal
```

Expected: All 11 tests PASS.

**Step 12: Commit**

```bash
git add src/Services/Identity/FairBank.Identity.Domain/ tests/FairBank.Identity.UnitTests/ FairBank.sln
git commit -m "feat(identity): add Domain layer with User entity, Email value object, and ports"
```

---

## Task 5: Identity Service — Application Layer (MediatR CQRS)

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Application/FairBank.Identity.Application.csproj`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandValidator.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetUserById/GetUserByIdQuery.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetUserById/GetUserByIdQueryHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/UserResponse.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/DependencyInjection.cs`
- Create: `tests/FairBank.Identity.UnitTests/Application/RegisterUserCommandHandlerTests.cs`

**Step 1: Create project**

```bash
cd /home/kamil/Job/fai
dotnet new classlib -n FairBank.Identity.Application -o src/Services/Identity/FairBank.Identity.Application
dotnet sln add src/Services/Identity/FairBank.Identity.Application/FairBank.Identity.Application.csproj
```

Remove `Class1.cs`.

**Step 2: Set up csproj**

`src/Services/Identity/FairBank.Identity.Application/FairBank.Identity.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="MediatR" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../FairBank.Identity.Domain/FairBank.Identity.Domain.csproj" />
  </ItemGroup>
</Project>
```

Add Application reference to test project:

In `tests/FairBank.Identity.UnitTests/FairBank.Identity.UnitTests.csproj` add:

```xml
<ProjectReference Include="../../src/Services/Identity/FairBank.Identity.Application/FairBank.Identity.Application.csproj" />
```

**Step 3: Create DTO**

`src/Services/Identity/FairBank.Identity.Application/Users/DTOs/UserResponse.cs`:

```csharp
using FairBank.Identity.Domain.Enums;

namespace FairBank.Identity.Application.Users.DTOs;

public sealed record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt);
```

**Step 4: Create RegisterUser command + validator**

`src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommand.cs`:

```csharp
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Enums;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    UserRole Role = UserRole.Client) : IRequest<UserResponse>;
```

`src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandValidator.cs`:

```csharp
using FluentValidation;

namespace FairBank.Identity.Application.Users.Commands.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches(@"\d").WithMessage("Password must contain a digit.")
            .Matches(@"[^a-zA-Z\d]").WithMessage("Password must contain a special character.");
    }
}
```

**Step 5: Write failing test for handler**

`tests/FairBank.Identity.UnitTests/Application/RegisterUserCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using FairBank.Identity.Application.Users.Commands.RegisterUser;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;

namespace FairBank.Identity.UnitTests.Application;

public class RegisterUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateUser()
    {
        // Arrange
        _userRepository.ExistsWithEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new RegisterUserCommandHandler(_userRepository, _unitOfWork);
        var command = new RegisterUserCommand("Jan", "Novák", "jan@example.com", "Password1!", UserRole.Client);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FirstName.Should().Be("Jan");
        result.LastName.Should().Be("Novák");
        result.Email.Should().Be("jan@example.com");
        result.Role.Should().Be(UserRole.Client);

        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldThrow()
    {
        // Arrange
        _userRepository.ExistsWithEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new RegisterUserCommandHandler(_userRepository, _unitOfWork);
        var command = new RegisterUserCommand("Jan", "Novák", "jan@example.com", "Password1!", UserRole.Client);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }
}
```

**Step 6: Run tests — verify they fail**

```bash
dotnet test tests/FairBank.Identity.UnitTests/ --verbosity normal
```

Expected: FAIL — `RegisterUserCommandHandler` does not exist.

**Step 7: Implement handler**

`src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandHandler.cs`:

```csharp
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterUserCommand, UserResponse>
{
    public async Task<UserResponse> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        var email = Email.Create(request.Email);

        if (await userRepository.ExistsWithEmailAsync(email, ct))
            throw new InvalidOperationException($"User with email '{request.Email}' already exists.");

        // NOTE: In production, hash with BCrypt/Argon2. Simplified for now.
        var passwordHash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(request.Password)));

        var user = User.Create(
            request.FirstName,
            request.LastName,
            email,
            passwordHash,
            request.Role);

        await userRepository.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new UserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email.Value,
            user.Role,
            user.IsActive,
            user.CreatedAt);
    }
}
```

**Step 8: Create GetUserById query**

`src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetUserById/GetUserByIdQuery.cs`:

```csharp
using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<UserResponse?>;
```

`src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetUserById/GetUserByIdQueryHandler.cs`:

```csharp
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Users.Queries.GetUserById;

public sealed class GetUserByIdQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserByIdQuery, UserResponse?>
{
    public async Task<UserResponse?> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.Id, ct);

        if (user is null) return null;

        return new UserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email.Value,
            user.Role,
            user.IsActive,
            user.CreatedAt);
    }
}
```

**Step 9: Create DI registration**

`src/Services/Identity/FairBank.Identity.Application/DependencyInjection.cs`:

```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
```

**Step 10: Run all tests — verify they pass**

```bash
dotnet test tests/FairBank.Identity.UnitTests/ --verbosity normal
```

Expected: All 13 tests PASS.

**Step 11: Commit**

```bash
git add src/Services/Identity/FairBank.Identity.Application/ tests/FairBank.Identity.UnitTests/ FairBank.sln
git commit -m "feat(identity): add Application layer with RegisterUser command and GetUserById query"
```

---

## Task 6: Identity Service — Infrastructure Layer (EF Core + PostgreSQL)

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/FairBank.Identity.Infrastructure.csproj`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/IdentityDbContext.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/DependencyInjection.cs`

**Step 1: Create project**

```bash
cd /home/kamil/Job/fai
dotnet new classlib -n FairBank.Identity.Infrastructure -o src/Services/Identity/FairBank.Identity.Infrastructure
dotnet sln add src/Services/Identity/FairBank.Identity.Infrastructure/FairBank.Identity.Infrastructure.csproj
```

Remove `Class1.cs`.

**Step 2: Set up csproj**

`src/Services/Identity/FairBank.Identity.Infrastructure/FairBank.Identity.Infrastructure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../FairBank.Identity.Domain/FairBank.Identity.Domain.csproj" />
    <ProjectReference Include="../FairBank.Identity.Application/FairBank.Identity.Application.csproj" />
  </ItemGroup>
</Project>
```

**Step 3: Create DbContext**

`src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/IdentityDbContext.cs`:

```csharp
using FairBank.Identity.Domain.Entities;
using FairBank.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity_service");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
```

**Step 4: Create User EF configuration**

`src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs`:

```csharp
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(320)
                .IsRequired();

            email.HasIndex(e => e.Value).IsUnique();
        });

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.IsDeleted).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();

        // Global query filter: soft delete
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
```

**Step 5: Create UserRepository (driven adapter)**

`src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs`:

```csharp
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task AddAsync(User aggregate, CancellationToken ct = default)
    {
        await db.Users.AddAsync(aggregate, ct);
    }

    public Task UpdateAsync(User aggregate, CancellationToken ct = default)
    {
        db.Users.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<bool> ExistsWithEmailAsync(Email email, CancellationToken ct = default)
    {
        return await db.Users.AnyAsync(u => u.Email.Value == email.Value, ct);
    }
}
```

**Step 6: Create DI registration**

`src/Services/Identity/FairBank.Identity.Infrastructure/DependencyInjection.cs`:

```csharp
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Infrastructure.Persistence;
using FairBank.Identity.Infrastructure.Persistence.Repositories;
using FairBank.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity_service");
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            }));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());

        return services;
    }
}
```

**Step 7: Verify build**

```bash
dotnet build src/Services/Identity/FairBank.Identity.Infrastructure/
```

Expected: Build succeeded.

**Step 8: Commit**

```bash
git add src/Services/Identity/FairBank.Identity.Infrastructure/ FairBank.sln
git commit -m "feat(identity): add Infrastructure layer with EF Core DbContext, UserRepository, and soft delete filter"
```

---

## Task 7: Identity Service — API Layer (Minimal APIs)

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj`
- Create: `src/Services/Identity/FairBank.Identity.Api/Program.cs`
- Create: `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`
- Create: `src/Services/Identity/FairBank.Identity.Api/appsettings.json`
- Create: `src/Services/Identity/FairBank.Identity.Api/appsettings.Development.json`
- Create: `src/Services/Identity/FairBank.Identity.Api/Dockerfile`

**Step 1: Create project**

```bash
cd /home/kamil/Job/fai
dotnet new web -n FairBank.Identity.Api -o src/Services/Identity/FairBank.Identity.Api
dotnet sln add src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj
```

**Step 2: Set up csproj**

`src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Sinks.Console" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../FairBank.Identity.Application/FairBank.Identity.Application.csproj" />
    <ProjectReference Include="../FairBank.Identity.Infrastructure/FairBank.Identity.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

**Step 3: Create appsettings**

`src/Services/Identity/FairBank.Identity.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

`src/Services/Identity/FairBank.Identity.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=identity_service"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

**Step 4: Create endpoint definitions**

`src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`:

```csharp
using FairBank.Identity.Application.Users.Commands.RegisterUser;
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

        return group;
    }
}
```

**Step 5: Create Program.cs**

`src/Services/Identity/FairBank.Identity.Api/Program.cs`:

```csharp
using FairBank.Identity.Api.Endpoints;
using FairBank.Identity.Application;
using FairBank.Identity.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

// Application layer (MediatR, FluentValidation)
builder.Services.AddIdentityApplication();

// Infrastructure layer (EF Core, repositories)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
builder.Services.AddIdentityInfrastructure(connectionString);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

// Map endpoints
app.MapUserEndpoints();

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Identity" }))
    .WithTags("Health");

app.Run();

// Required for integration tests
public partial class Program;
```

**Step 6: Create Dockerfile**

`src/Services/Identity/FairBank.Identity.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .

COPY src/FairBank.SharedKernel/FairBank.SharedKernel.csproj src/FairBank.SharedKernel/
COPY src/Services/Identity/FairBank.Identity.Domain/FairBank.Identity.Domain.csproj src/Services/Identity/FairBank.Identity.Domain/
COPY src/Services/Identity/FairBank.Identity.Application/FairBank.Identity.Application.csproj src/Services/Identity/FairBank.Identity.Application/
COPY src/Services/Identity/FairBank.Identity.Infrastructure/FairBank.Identity.Infrastructure.csproj src/Services/Identity/FairBank.Identity.Infrastructure/
COPY src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj src/Services/Identity/FairBank.Identity.Api/

RUN dotnet restore src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj

COPY src/ src/
RUN dotnet publish src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.Identity.Api.dll"]
```

**Step 7: Add Identity service to docker-compose.yml**

Append to `docker-compose.yml` services section:

```yaml
  identity-api:
    build:
      context: .
      dockerfile: src/Services/Identity/FairBank.Identity.Api/Dockerfile
    container_name: fairbank-identity-api
    ports:
      - "8001:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=identity_service"
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - backend
```

**Step 8: Generate EF Core migration**

```bash
cd /home/kamil/Job/fai
dotnet ef migrations add InitialCreate \
    --project src/Services/Identity/FairBank.Identity.Infrastructure/ \
    --startup-project src/Services/Identity/FairBank.Identity.Api/ \
    --output-dir Persistence/Migrations
```

**Step 9: Test locally — run migration and start API**

```bash
docker compose up -d postgres
dotnet ef database update \
    --project src/Services/Identity/FairBank.Identity.Infrastructure/ \
    --startup-project src/Services/Identity/FairBank.Identity.Api/
dotnet run --project src/Services/Identity/FairBank.Identity.Api/
```

In a separate terminal, test:

```bash
# Health check
curl http://localhost:5000/health

# Register user
curl -X POST http://localhost:5000/api/v1/users/register \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Jan","lastName":"Novák","email":"jan@example.com","password":"Password1!"}'

# Get user (use ID from previous response)
curl http://localhost:5000/api/v1/users/{id}
```

**Step 10: Commit**

```bash
git add src/Services/Identity/FairBank.Identity.Api/ docker-compose.yml FairBank.sln
git commit -m "feat(identity): add API layer with Minimal API endpoints, Dockerfile, and Docker Compose integration"
```

---

## Task 8: Accounts Service — Domain Layer (Event Sourcing)

**Files:**
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/FairBank.Accounts.Domain.csproj`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/Account.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/AccountCreated.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/MoneyDeposited.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/MoneyWithdrawn.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/ValueObjects/Money.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/ValueObjects/AccountNumber.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Enums/Currency.cs`
- Create: `tests/FairBank.Accounts.UnitTests/FairBank.Accounts.UnitTests.csproj`
- Create: `tests/FairBank.Accounts.UnitTests/Domain/AccountTests.cs`
- Create: `tests/FairBank.Accounts.UnitTests/Domain/MoneyTests.cs`

**Step 1: Create projects**

```bash
cd /home/kamil/Job/fai
dotnet new classlib -n FairBank.Accounts.Domain -o src/Services/Accounts/FairBank.Accounts.Domain
dotnet sln add src/Services/Accounts/FairBank.Accounts.Domain/FairBank.Accounts.Domain.csproj

dotnet new xunit -n FairBank.Accounts.UnitTests -o tests/FairBank.Accounts.UnitTests
dotnet sln add tests/FairBank.Accounts.UnitTests/FairBank.Accounts.UnitTests.csproj
```

Remove auto-generated files.

**Step 2: Set up csproj files**

`src/Services/Accounts/FairBank.Accounts.Domain/FairBank.Accounts.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../../../FairBank.SharedKernel/FairBank.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

`tests/FairBank.Accounts.UnitTests/FairBank.Accounts.UnitTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Services/Accounts/FairBank.Accounts.Domain/FairBank.Accounts.Domain.csproj" />
  </ItemGroup>
</Project>
```

**Step 3: Create Currency enum and value objects**

`src/Services/Accounts/FairBank.Accounts.Domain/Enums/Currency.cs`:

```csharp
namespace FairBank.Accounts.Domain.Enums;

public enum Currency
{
    CZK,
    EUR,
    USD,
    GBP
}
```

`src/Services/Accounts/FairBank.Accounts.Domain/ValueObjects/Money.cs`:

```csharp
using FairBank.Accounts.Domain.Enums;
using FairBank.SharedKernel.Domain;

namespace FairBank.Accounts.Domain.ValueObjects;

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Create(decimal amount, Currency currency)
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));

        return new Money(Math.Round(amount, 2), currency);
    }

    public static Money Zero(Currency currency) => new(0, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        if (Amount < other.Amount)
            throw new InvalidOperationException("Insufficient funds.");
        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot operate on different currencies: {Currency} vs {other.Currency}.");
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}
```

`src/Services/Accounts/FairBank.Accounts.Domain/ValueObjects/AccountNumber.cs`:

```csharp
using FairBank.SharedKernel.Domain;

namespace FairBank.Accounts.Domain.ValueObjects;

public sealed class AccountNumber : ValueObject
{
    public string Value { get; }

    private AccountNumber(string value) => Value = value;

    public static AccountNumber Create(string? value = null)
    {
        var number = value ?? GenerateAccountNumber();
        return new AccountNumber(number);
    }

    private static string GenerateAccountNumber()
    {
        var random = Random.Shared;
        // Format: FAIR-XXXX-XXXX-XXXX (16 digits)
        return $"FAIR-{random.Next(1000, 9999)}-{random.Next(1000, 9999)}-{random.Next(1000, 9999)}";
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
```

**Step 4: Create domain events**

`src/Services/Accounts/FairBank.Accounts.Domain/Events/AccountCreated.cs`:

```csharp
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record AccountCreated(
    Guid AccountId,
    Guid OwnerId,
    string AccountNumber,
    Currency Currency,
    DateTime OccurredAt);
```

`src/Services/Accounts/FairBank.Accounts.Domain/Events/MoneyDeposited.cs`:

```csharp
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record MoneyDeposited(
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description,
    DateTime OccurredAt);
```

`src/Services/Accounts/FairBank.Accounts.Domain/Events/MoneyWithdrawn.cs`:

```csharp
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Domain.Events;

public sealed record MoneyWithdrawn(
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description,
    DateTime OccurredAt);
```

**Step 5: Write failing tests for Money**

`tests/FairBank.Accounts.UnitTests/Domain/MoneyTests.cs`:

```csharp
using FluentAssertions;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Create_WithValidAmount_ShouldSucceed()
    {
        var money = Money.Create(100.50m, Currency.CZK);
        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be(Currency.CZK);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrow()
    {
        var act = () => Money.Create(-1, Currency.CZK);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_SameCurrency_ShouldReturnSum()
    {
        var a = Money.Create(100, Currency.CZK);
        var b = Money.Create(50, Currency.CZK);
        var result = a.Add(b);
        result.Amount.Should().Be(150);
    }

    [Fact]
    public void Add_DifferentCurrency_ShouldThrow()
    {
        var czk = Money.Create(100, Currency.CZK);
        var eur = Money.Create(50, Currency.EUR);
        var act = () => czk.Add(eur);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Subtract_WithSufficientFunds_ShouldSucceed()
    {
        var a = Money.Create(100, Currency.CZK);
        var b = Money.Create(30, Currency.CZK);
        var result = a.Subtract(b);
        result.Amount.Should().Be(70);
    }

    [Fact]
    public void Subtract_WithInsufficientFunds_ShouldThrow()
    {
        var a = Money.Create(10, Currency.CZK);
        var b = Money.Create(50, Currency.CZK);
        var act = () => a.Subtract(b);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient*");
    }
}
```

**Step 6: Run tests — verify they pass**

```bash
dotnet test tests/FairBank.Accounts.UnitTests/ --verbosity normal
```

Expected: All 6 Money tests PASS.

**Step 7: Write failing tests for Account aggregate**

`tests/FairBank.Accounts.UnitTests/Domain/AccountTests.cs`:

```csharp
using FluentAssertions;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Domain;

public class AccountTests
{
    [Fact]
    public void Create_ShouldInitializeWithZeroBalance()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);

        account.Id.Should().NotBe(Guid.Empty);
        account.Balance.Amount.Should().Be(0);
        account.Balance.Currency.Should().Be(Currency.CZK);
        account.AccountNumber.Should().NotBeNull();
        account.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldRaiseAccountCreatedEvent()
    {
        var ownerId = Guid.NewGuid();
        var account = Account.Create(ownerId, Currency.CZK);

        var events = account.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<AccountCreated>();

        var evt = (AccountCreated)events[0];
        evt.OwnerId.Should().Be(ownerId);
        evt.Currency.Should().Be(Currency.CZK);
    }

    [Fact]
    public void Deposit_ShouldIncreaseBalance()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);

        account.Deposit(Money.Create(500, Currency.CZK), "Initial deposit");

        account.Balance.Amount.Should().Be(500);
    }

    [Fact]
    public void Deposit_ShouldRaiseMoneyDepositedEvent()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);

        account.Deposit(Money.Create(500, Currency.CZK), "Deposit");

        var events = account.GetUncommittedEvents();
        events.Should().HaveCount(2); // AccountCreated + MoneyDeposited
        events[1].Should().BeOfType<MoneyDeposited>();
    }

    [Fact]
    public void Withdraw_WithSufficientFunds_ShouldDecreaseBalance()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.Deposit(Money.Create(1000, Currency.CZK), "Deposit");

        account.Withdraw(Money.Create(300, Currency.CZK), "ATM withdrawal");

        account.Balance.Amount.Should().Be(700);
    }

    [Fact]
    public void Withdraw_WithInsufficientFunds_ShouldThrow()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.Deposit(Money.Create(100, Currency.CZK), "Deposit");

        var act = () => account.Withdraw(Money.Create(500, Currency.CZK), "Too much");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public void Withdraw_FromInactiveAccount_ShouldThrow()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        account.Deposit(Money.Create(1000, Currency.CZK), "Deposit");
        account.Deactivate();

        var act = () => account.Withdraw(Money.Create(100, Currency.CZK), "Attempt");

        act.Should().Throw<InvalidOperationException>().WithMessage("*not active*");
    }
}
```

**Step 8: Run tests — verify they fail**

```bash
dotnet test tests/FairBank.Accounts.UnitTests/ --verbosity normal
```

Expected: FAIL — `Account` class does not exist.

**Step 9: Implement Account aggregate (Event Sourcing style)**

`src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/Account.cs`:

```csharp
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.Domain.Aggregates;

public sealed class Account
{
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public AccountNumber AccountNumber { get; private set; } = null!;
    public Money Balance { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<object> _uncommittedEvents = [];

    private Account() { } // Marten rehydration

    public static Account Create(Guid ownerId, Currency currency)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            AccountNumber = AccountNumber.Create(),
            Balance = Money.Zero(currency),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        account.RaiseEvent(new AccountCreated(
            account.Id,
            ownerId,
            account.AccountNumber.Value,
            currency,
            DateTime.UtcNow));

        return account;
    }

    public void Deposit(Money amount, string description)
    {
        EnsureActive();

        Balance = Balance.Add(amount);

        RaiseEvent(new MoneyDeposited(
            Id,
            amount.Amount,
            amount.Currency,
            description,
            DateTime.UtcNow));
    }

    public void Withdraw(Money amount, string description)
    {
        EnsureActive();

        Balance = Balance.Subtract(amount); // Throws if insufficient

        RaiseEvent(new MoneyWithdrawn(
            Id,
            amount.Amount,
            amount.Currency,
            description,
            DateTime.UtcNow));
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    // --- Event Sourcing support ---

    public IReadOnlyList<object> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    private void RaiseEvent(object @event) => _uncommittedEvents.Add(@event);

    private void EnsureActive()
    {
        if (!IsActive)
            throw new InvalidOperationException("Account is not active.");
    }

    // Marten event sourcing: Apply methods for rehydration from events
    public void Apply(AccountCreated @event)
    {
        Id = @event.AccountId;
        OwnerId = @event.OwnerId;
        AccountNumber = AccountNumber.Create(@event.AccountNumber);
        Balance = Money.Zero(@event.Currency);
        IsActive = true;
        CreatedAt = @event.OccurredAt;
    }

    public void Apply(MoneyDeposited @event)
    {
        Balance = Balance.Add(Money.Create(@event.Amount, @event.Currency));
    }

    public void Apply(MoneyWithdrawn @event)
    {
        Balance = Balance.Subtract(Money.Create(@event.Amount, @event.Currency));
    }
}
```

**Step 10: Run all tests — verify they pass**

```bash
dotnet test tests/FairBank.Accounts.UnitTests/ --verbosity normal
```

Expected: All 13 tests PASS (6 Money + 7 Account).

**Step 11: Commit**

```bash
git add src/Services/Accounts/FairBank.Accounts.Domain/ tests/FairBank.Accounts.UnitTests/ FairBank.sln
git commit -m "feat(accounts): add Domain layer with Account aggregate (event sourcing), Money value object, and domain events"
```

---

## Task 9: Accounts Service — Application + Infrastructure + API (Marten)

**Files:**
- Create: `src/Services/Accounts/FairBank.Accounts.Application/FairBank.Accounts.Application.csproj`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/CreateAccount/CreateAccountCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/CreateAccount/CreateAccountCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/DepositMoney/DepositMoneyCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/DepositMoney/DepositMoneyCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/WithdrawMoney/WithdrawMoneyCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/WithdrawMoney/WithdrawMoneyCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetAccountById/GetAccountByIdQuery.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetAccountById/GetAccountByIdQueryHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/DTOs/AccountResponse.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Ports/IAccountEventStore.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/DependencyInjection.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Infrastructure/FairBank.Accounts.Infrastructure.csproj`
- Create: `src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/MartenAccountEventStore.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Infrastructure/DependencyInjection.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Api/FairBank.Accounts.Api.csproj`
- Create: `src/Services/Accounts/FairBank.Accounts.Api/Program.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Api/Endpoints/AccountEndpoints.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Api/appsettings.json`
- Create: `src/Services/Accounts/FairBank.Accounts.Api/appsettings.Development.json`
- Create: `src/Services/Accounts/FairBank.Accounts.Api/Dockerfile`

**Step 1: Create all projects**

```bash
cd /home/kamil/Job/fai

dotnet new classlib -n FairBank.Accounts.Application -o src/Services/Accounts/FairBank.Accounts.Application
dotnet new classlib -n FairBank.Accounts.Infrastructure -o src/Services/Accounts/FairBank.Accounts.Infrastructure
dotnet new web -n FairBank.Accounts.Api -o src/Services/Accounts/FairBank.Accounts.Api

dotnet sln add src/Services/Accounts/FairBank.Accounts.Application/FairBank.Accounts.Application.csproj
dotnet sln add src/Services/Accounts/FairBank.Accounts.Infrastructure/FairBank.Accounts.Infrastructure.csproj
dotnet sln add src/Services/Accounts/FairBank.Accounts.Api/FairBank.Accounts.Api.csproj
```

Remove auto-generated files.

**Step 2: Set up csproj files**

`src/Services/Accounts/FairBank.Accounts.Application/FairBank.Accounts.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="MediatR" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../FairBank.Accounts.Domain/FairBank.Accounts.Domain.csproj" />
  </ItemGroup>
</Project>
```

`src/Services/Accounts/FairBank.Accounts.Infrastructure/FairBank.Accounts.Infrastructure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Marten" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../FairBank.Accounts.Domain/FairBank.Accounts.Domain.csproj" />
    <ProjectReference Include="../FairBank.Accounts.Application/FairBank.Accounts.Application.csproj" />
  </ItemGroup>
</Project>
```

`src/Services/Accounts/FairBank.Accounts.Api/FairBank.Accounts.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Sinks.Console" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../FairBank.Accounts.Application/FairBank.Accounts.Application.csproj" />
    <ProjectReference Include="../FairBank.Accounts.Infrastructure/FairBank.Accounts.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

**Step 3: Create port (event store interface)**

`src/Services/Accounts/FairBank.Accounts.Application/Ports/IAccountEventStore.cs`:

```csharp
using FairBank.Accounts.Domain.Aggregates;

namespace FairBank.Accounts.Application.Ports;

public interface IAccountEventStore
{
    Task<Account?> LoadAsync(Guid accountId, CancellationToken ct = default);
    Task AppendEventsAsync(Account account, CancellationToken ct = default);
}
```

**Step 4: Create DTOs**

`src/Services/Accounts/FairBank.Accounts.Application/DTOs/AccountResponse.cs`:

```csharp
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Application.DTOs;

public sealed record AccountResponse(
    Guid Id,
    Guid OwnerId,
    string AccountNumber,
    decimal Balance,
    Currency Currency,
    bool IsActive,
    DateTime CreatedAt);
```

**Step 5: Create commands and handlers**

`src/Services/Accounts/FairBank.Accounts.Application/Commands/CreateAccount/CreateAccountCommand.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreateAccount;

public sealed record CreateAccountCommand(
    Guid OwnerId,
    Currency Currency = Currency.CZK) : IRequest<AccountResponse>;
```

`src/Services/Accounts/FairBank.Accounts.Application/Commands/CreateAccount/CreateAccountCommandHandler.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreateAccount;

public sealed class CreateAccountCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<CreateAccountCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(CreateAccountCommand request, CancellationToken ct)
    {
        var account = Account.Create(request.OwnerId, request.Currency);

        await eventStore.AppendEventsAsync(account, ct);

        return new AccountResponse(
            account.Id,
            account.OwnerId,
            account.AccountNumber.Value,
            account.Balance.Amount,
            account.Balance.Currency,
            account.IsActive,
            account.CreatedAt);
    }
}
```

`src/Services/Accounts/FairBank.Accounts.Application/Commands/DepositMoney/DepositMoneyCommand.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.DepositMoney;

public sealed record DepositMoneyCommand(
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description) : IRequest<AccountResponse>;
```

`src/Services/Accounts/FairBank.Accounts.Application/Commands/DepositMoney/DepositMoneyCommandHandler.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.DepositMoney;

public sealed class DepositMoneyCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<DepositMoneyCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(DepositMoneyCommand request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException($"Account {request.AccountId} not found.");

        account.Deposit(Money.Create(request.Amount, request.Currency), request.Description);

        await eventStore.AppendEventsAsync(account, ct);

        return new AccountResponse(
            account.Id,
            account.OwnerId,
            account.AccountNumber.Value,
            account.Balance.Amount,
            account.Balance.Currency,
            account.IsActive,
            account.CreatedAt);
    }
}
```

`src/Services/Accounts/FairBank.Accounts.Application/Commands/WithdrawMoney/WithdrawMoneyCommand.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.WithdrawMoney;

public sealed record WithdrawMoneyCommand(
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description) : IRequest<AccountResponse>;
```

`src/Services/Accounts/FairBank.Accounts.Application/Commands/WithdrawMoney/WithdrawMoneyCommandHandler.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.WithdrawMoney;

public sealed class WithdrawMoneyCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<WithdrawMoneyCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(WithdrawMoneyCommand request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException($"Account {request.AccountId} not found.");

        account.Withdraw(Money.Create(request.Amount, request.Currency), request.Description);

        await eventStore.AppendEventsAsync(account, ct);

        return new AccountResponse(
            account.Id,
            account.OwnerId,
            account.AccountNumber.Value,
            account.Balance.Amount,
            account.Balance.Currency,
            account.IsActive,
            account.CreatedAt);
    }
}
```

**Step 6: Create GetAccountById query**

`src/Services/Accounts/FairBank.Accounts.Application/Queries/GetAccountById/GetAccountByIdQuery.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountById;

public sealed record GetAccountByIdQuery(Guid AccountId) : IRequest<AccountResponse?>;
```

`src/Services/Accounts/FairBank.Accounts.Application/Queries/GetAccountById/GetAccountByIdQueryHandler.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountById;

public sealed class GetAccountByIdQueryHandler(IAccountEventStore eventStore)
    : IRequestHandler<GetAccountByIdQuery, AccountResponse?>
{
    public async Task<AccountResponse?> Handle(GetAccountByIdQuery request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct);

        if (account is null) return null;

        return new AccountResponse(
            account.Id,
            account.OwnerId,
            account.AccountNumber.Value,
            account.Balance.Amount,
            account.Balance.Currency,
            account.IsActive,
            account.CreatedAt);
    }
}
```

**Step 7: Create DI for Application**

`src/Services/Accounts/FairBank.Accounts.Application/DependencyInjection.cs`:

```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Accounts.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountsApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
```

**Step 8: Implement Marten event store adapter**

`src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/MartenAccountEventStore.cs`:

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

    public async Task AppendEventsAsync(Account account, CancellationToken ct = default)
    {
        var events = account.GetUncommittedEvents();

        if (events.Count == 0) return;

        var stream = session.Events.StartStream<Account>(account.Id, events.ToArray());
        account.ClearUncommittedEvents();

        await session.SaveChangesAsync(ct);
    }
}
```

**Step 9: Create DI for Infrastructure**

`src/Services/Accounts/FairBank.Accounts.Infrastructure/DependencyInjection.cs`:

```csharp
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Infrastructure.Persistence;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using Weasel.Core;

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

            // Register aggregate for event sourcing
            options.Projections.Snapshot<Account>(SnapshotLifecycle.Inline);
        })
        .UseLightweightSessions();

        services.AddScoped<IAccountEventStore, MartenAccountEventStore>();

        return services;
    }
}
```

**Step 10: Create API layer**

`src/Services/Accounts/FairBank.Accounts.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

`src/Services/Accounts/FairBank.Accounts.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=accounts_service"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

`src/Services/Accounts/FairBank.Accounts.Api/Endpoints/AccountEndpoints.cs`:

```csharp
using FairBank.Accounts.Application.Commands.CreateAccount;
using FairBank.Accounts.Application.Commands.DepositMoney;
using FairBank.Accounts.Application.Commands.WithdrawMoney;
using FairBank.Accounts.Application.Queries.GetAccountById;
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

        return group;
    }
}
```

`src/Services/Accounts/FairBank.Accounts.Api/Program.cs`:

```csharp
using FairBank.Accounts.Api.Endpoints;
using FairBank.Accounts.Application;
using FairBank.Accounts.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddAccountsApplication();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
builder.Services.AddAccountsInfrastructure(connectionString);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.MapAccountEndpoints();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Accounts" }))
    .WithTags("Health");

app.Run();

public partial class Program;
```

**Step 11: Create Dockerfile**

`src/Services/Accounts/FairBank.Accounts.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .

COPY src/FairBank.SharedKernel/FairBank.SharedKernel.csproj src/FairBank.SharedKernel/
COPY src/Services/Accounts/FairBank.Accounts.Domain/FairBank.Accounts.Domain.csproj src/Services/Accounts/FairBank.Accounts.Domain/
COPY src/Services/Accounts/FairBank.Accounts.Application/FairBank.Accounts.Application.csproj src/Services/Accounts/FairBank.Accounts.Application/
COPY src/Services/Accounts/FairBank.Accounts.Infrastructure/FairBank.Accounts.Infrastructure.csproj src/Services/Accounts/FairBank.Accounts.Infrastructure/
COPY src/Services/Accounts/FairBank.Accounts.Api/FairBank.Accounts.Api.csproj src/Services/Accounts/FairBank.Accounts.Api/

RUN dotnet restore src/Services/Accounts/FairBank.Accounts.Api/FairBank.Accounts.Api.csproj

COPY src/ src/
RUN dotnet publish src/Services/Accounts/FairBank.Accounts.Api/FairBank.Accounts.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.Accounts.Api.dll"]
```

**Step 12: Add Accounts service to docker-compose.yml**

```yaml
  accounts-api:
    build:
      context: .
      dockerfile: src/Services/Accounts/FairBank.Accounts.Api/Dockerfile
    container_name: fairbank-accounts-api
    ports:
      - "8002:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=accounts_service"
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - backend
```

**Step 13: Build and test everything**

```bash
dotnet build
dotnet test --verbosity normal
docker compose build
docker compose up -d
```

Test the APIs:

```bash
# Identity - register user
curl -X POST http://localhost:8001/api/v1/users/register \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Jan","lastName":"Novák","email":"jan@example.com","password":"Password1!"}'

# Accounts - create account (use userId from above)
curl -X POST http://localhost:8002/api/v1/accounts \
  -H "Content-Type: application/json" \
  -d '{"ownerId":"<USER_ID>","currency":"CZK"}'

# Accounts - deposit
curl -X POST http://localhost:8002/api/v1/accounts/<ACCOUNT_ID>/deposit \
  -H "Content-Type: application/json" \
  -d '{"amount":1000,"currency":"CZK","description":"Initial deposit"}'

# Accounts - get balance
curl http://localhost:8002/api/v1/accounts/<ACCOUNT_ID>

# Accounts - withdraw
curl -X POST http://localhost:8002/api/v1/accounts/<ACCOUNT_ID>/withdraw \
  -H "Content-Type: application/json" \
  -d '{"amount":250,"currency":"CZK","description":"Coffee"}'
```

**Step 14: Commit**

```bash
git add src/Services/Accounts/ docker-compose.yml FairBank.sln
git commit -m "feat(accounts): add Accounts service with Marten event sourcing, Minimal API endpoints, and Docker integration"
```

---

## Task 10: API Gateway (YARP)

**Files:**
- Create: `src/FairBank.ApiGateway/FairBank.ApiGateway.csproj`
- Create: `src/FairBank.ApiGateway/Program.cs`
- Create: `src/FairBank.ApiGateway/appsettings.json`
- Create: `src/FairBank.ApiGateway/appsettings.Development.json`
- Create: `src/FairBank.ApiGateway/Dockerfile`

**Step 1: Create project**

```bash
cd /home/kamil/Job/fai
dotnet new web -n FairBank.ApiGateway -o src/FairBank.ApiGateway
dotnet sln add src/FairBank.ApiGateway/FairBank.ApiGateway.csproj
```

**Step 2: Set up csproj**

`src/FairBank.ApiGateway/FairBank.ApiGateway.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="Yarp.ReverseProxy" />
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Sinks.Console" />
  </ItemGroup>
</Project>
```

**Step 3: Create configuration**

`src/FairBank.ApiGateway/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ReverseProxy": {
    "Routes": {
      "identity-route": {
        "ClusterId": "identity-cluster",
        "Match": {
          "Path": "/api/v1/users/{**catch-all}"
        }
      },
      "accounts-route": {
        "ClusterId": "accounts-cluster",
        "Match": {
          "Path": "/api/v1/accounts/{**catch-all}"
        }
      },
      "identity-health": {
        "ClusterId": "identity-cluster",
        "Match": {
          "Path": "/identity/health"
        },
        "Transforms": [
          { "PathRemovePrefix": "/identity" }
        ]
      },
      "accounts-health": {
        "ClusterId": "accounts-cluster",
        "Match": {
          "Path": "/accounts/health"
        },
        "Transforms": [
          { "PathRemovePrefix": "/accounts" }
        ]
      }
    },
    "Clusters": {
      "identity-cluster": {
        "Destinations": {
          "identity-api": {
            "Address": "http://identity-api:8080"
          }
        }
      },
      "accounts-cluster": {
        "Destinations": {
          "accounts-api": {
            "Address": "http://accounts-api:8080"
          }
        }
      }
    }
  }
}
```

`src/FairBank.ApiGateway/appsettings.Development.json`:

```json
{
  "ReverseProxy": {
    "Clusters": {
      "identity-cluster": {
        "Destinations": {
          "identity-api": {
            "Address": "http://localhost:8001"
          }
        }
      },
      "accounts-cluster": {
        "Destinations": {
          "accounts-api": {
            "Address": "http://localhost:8002"
          }
        }
      }
    }
  }
}
```

**Step 4: Create Program.cs**

`src/FairBank.ApiGateway/Program.cs`:

```csharp
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapReverseProxy();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ApiGateway" }));

app.Run();
```

**Step 5: Create Dockerfile**

`src/FairBank.ApiGateway/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/FairBank.ApiGateway/FairBank.ApiGateway.csproj src/FairBank.ApiGateway/

RUN dotnet restore src/FairBank.ApiGateway/FairBank.ApiGateway.csproj

COPY src/FairBank.ApiGateway/ src/FairBank.ApiGateway/
RUN dotnet publish src/FairBank.ApiGateway/FairBank.ApiGateway.csproj -c Release -o /app/publish --no-restore

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.ApiGateway.dll"]
```

**Step 6: Add Gateway to docker-compose.yml**

```yaml
  api-gateway:
    build:
      context: .
      dockerfile: src/FairBank.ApiGateway/Dockerfile
    container_name: fairbank-api-gateway
    ports:
      - "5000:8080"
    depends_on:
      - identity-api
      - accounts-api
    networks:
      - backend
```

**Step 7: Full integration test**

```bash
docker compose build
docker compose up -d

# Test through gateway
curl http://localhost:5000/health
curl http://localhost:5000/api/v1/users/register \
  -X POST -H "Content-Type: application/json" \
  -d '{"firstName":"Jan","lastName":"Novák","email":"jan@example.com","password":"Password1!"}'
curl http://localhost:5000/api/v1/accounts \
  -X POST -H "Content-Type: application/json" \
  -d '{"ownerId":"<USER_ID>","currency":"CZK"}'
```

**Step 8: Commit**

```bash
git add src/FairBank.ApiGateway/ docker-compose.yml FairBank.sln
git commit -m "feat: add YARP API Gateway routing to Identity and Accounts services"
```

---

## Final Docker Compose (complete)

After all tasks, `docker-compose.yml` should contain:

```yaml
services:
  postgres:
    image: postgres:16-alpine
    container_name: fairbank-postgres
    environment:
      POSTGRES_DB: fairbank
      POSTGRES_USER: fairbank_admin
      POSTGRES_PASSWORD: fairbank_secret_2026
      POSTGRES_INITDB_ARGS: "--data-checksums"
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./docker/postgres/init.sql:/docker-entrypoint-initdb.d/01-init.sql:ro
    command: >
      postgres
        -c shared_buffers=128MB
        -c effective_cache_size=384MB
        -c work_mem=8MB
        -c log_statement=all
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U fairbank_admin -d fairbank"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - backend

  identity-api:
    build:
      context: .
      dockerfile: src/Services/Identity/FairBank.Identity.Api/Dockerfile
    container_name: fairbank-identity-api
    ports:
      - "8001:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=identity_service"
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - backend

  accounts-api:
    build:
      context: .
      dockerfile: src/Services/Accounts/FairBank.Accounts.Api/Dockerfile
    container_name: fairbank-accounts-api
    ports:
      - "8002:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=accounts_service"
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - backend

  api-gateway:
    build:
      context: .
      dockerfile: src/FairBank.ApiGateway/Dockerfile
    container_name: fairbank-api-gateway
    ports:
      - "5000:8080"
    depends_on:
      - identity-api
      - accounts-api
    networks:
      - backend

networks:
  backend:
    driver: bridge

volumes:
  pgdata:
```

---

## Summary of API Endpoints

### Identity Service (port 8001, via gateway: /api/v1/users)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/users/register` | Register new user |
| GET | `/api/v1/users/{id}` | Get user by ID |
| GET | `/health` | Health check |

### Accounts Service (port 8002, via gateway: /api/v1/accounts)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/accounts` | Create new account |
| GET | `/api/v1/accounts/{id}` | Get account with balance |
| POST | `/api/v1/accounts/{id}/deposit` | Deposit money |
| POST | `/api/v1/accounts/{id}/withdraw` | Withdraw money |
| GET | `/health` | Health check |

### API Gateway (port 5000)

Routes all `/api/v1/users/**` → Identity, `/api/v1/accounts/**` → Accounts.
