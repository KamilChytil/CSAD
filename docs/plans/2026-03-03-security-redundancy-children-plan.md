# Security, DB Redundancy & Child Accounts — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add JWT authentication, RBAC, PostgreSQL replication, child accounts with parental oversight, security hardening (rate limiting, CORS, audit log), and fix the Marten event store bug.

**Architecture:** JWT issued by Identity service, validated by API Gateway. PostgreSQL primary-replica streaming replication. Parent-child self-reference in Identity domain. Spending limits and pending transaction approval in Accounts domain. Security middleware at gateway level.

**Tech Stack:** BCrypt.Net-Next, Microsoft.AspNetCore.Authentication.JwtBearer, System.IdentityModel.Tokens.Jwt, ASP.NET Core Rate Limiting, PostgreSQL 16 streaming replication.

---

### Task 1: Fix Marten EventStore Bug (AppendToStream vs StartStream)

**Files:**
- Modify: `src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/MartenAccountEventStore.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Application/Ports/IAccountEventStore.cs`

**Step 1: Fix `AppendEventsAsync` to distinguish new vs existing streams**

In `MartenAccountEventStore.cs`, the current code always calls `StartStream` which fails for existing accounts (deposit/withdraw). Fix:

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

**Step 2: Update the `IAccountEventStore` interface**

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

**Step 3: Update command handlers to use correct method**

- `CreateAccountCommandHandler` → call `StartStreamAsync` (new stream)
- `DepositMoneyCommandHandler` → call `AppendEventsAsync` (existing stream)
- `WithdrawMoneyCommandHandler` → call `AppendEventsAsync` (existing stream)

Find handlers at:
- `src/Services/Accounts/FairBank.Accounts.Application/Commands/CreateAccount/CreateAccountCommandHandler.cs`
- `src/Services/Accounts/FairBank.Accounts.Application/Commands/DepositMoney/DepositMoneyCommandHandler.cs`
- `src/Services/Accounts/FairBank.Accounts.Application/Commands/WithdrawMoney/WithdrawMoneyCommandHandler.cs`

In each handler, change `await eventStore.AppendEventsAsync(account, ct)` to:
- **CreateAccount**: `await eventStore.StartStreamAsync(account, ct);`
- **Deposit/Withdraw**: keep `await eventStore.AppendEventsAsync(account, ct);`

**Step 4: Run tests to verify**

Run: `dotnet test FairBank.slnx`
Expected: All 40 tests pass (NSubstitute mocks won't break since interface changed)

**Step 5: Commit**

```bash
git add -A && git commit -m "fix: separate StartStream and AppendToStream in Marten event store"
```

---

### Task 2: Add New NuGet Packages to Directory.Packages.props

**Files:**
- Modify: `Directory.Packages.props`

**Step 1: Add BCrypt and JWT packages**

Add these lines to the `<ItemGroup>` in `Directory.Packages.props`:

```xml
    <!-- Authentication -->
    <PackageVersion Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.3" />
    <PackageVersion Include="System.IdentityModel.Tokens.Jwt" Version="8.8.0" />
```

**Step 2: Verify build**

Run: `dotnet build FairBank.slnx`
Expected: Build succeeded, 0 errors

**Step 3: Commit**

```bash
git add Directory.Packages.props && git commit -m "chore: add BCrypt and JWT packages to central package management"
```

---

### Task 3: RefreshToken Entity + JWT Token Service (Identity Domain & Application)

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Domain/Entities/RefreshToken.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/Ports/IRefreshTokenRepository.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Auth/IJwtTokenService.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Auth/JwtTokenService.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Auth/JwtSettings.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Auth/DTOs/LoginResponse.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs` — add `VerifyPassword` and `ParentId`
- Test: `tests/FairBank.Identity.UnitTests/Domain/RefreshTokenTests.cs`

**Step 1: Create `RefreshToken` entity**

Create `src/Services/Identity/FairBank.Identity.Domain/Entities/RefreshToken.cs`:

```csharp
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Entities;

public sealed class RefreshToken : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RefreshToken() { } // EF Core

    public static RefreshToken Create(Guid userId, TimeSpan lifetime)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsValid => !IsRevoked && !IsExpired;

    public void Revoke()
    {
        IsRevoked = true;
    }
}
```

**Step 2: Create `IRefreshTokenRepository`**

Create `src/Services/Identity/FairBank.Identity.Domain/Ports/IRefreshTokenRepository.cs`:

```csharp
using FairBank.Identity.Domain.Entities;

namespace FairBank.Identity.Domain.Ports;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default);
    Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct = default);
}
```

**Step 3: Add `ParentId` and password methods to `User`**

Modify `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs` — add after `DeletedAt` property (line 18):

```csharp
    public Guid? ParentId { get; private set; }
    public User? Parent { get; private set; }
    private readonly List<User> _children = [];
    public IReadOnlyCollection<User> Children => _children.AsReadOnly();
```

Add new factory method for child creation after the existing `Create` method:

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

**Step 4: Create `JwtSettings`**

Create `src/Services/Identity/FairBank.Identity.Application/Auth/JwtSettings.cs`:

```csharp
namespace FairBank.Identity.Application.Auth;

public sealed class JwtSettings
{
    public string Secret { get; init; } = null!;
    public string Issuer { get; init; } = "fairbank-identity";
    public string Audience { get; init; } = "fairbank-api";
    public int AccessTokenExpirationMinutes { get; init; } = 15;
    public int RefreshTokenExpirationDays { get; init; } = 7;
}
```

**Step 5: Create `LoginResponse` DTO**

Create `src/Services/Identity/FairBank.Identity.Application/Auth/DTOs/LoginResponse.cs`:

```csharp
namespace FairBank.Identity.Application.Auth.DTOs;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);
```

**Step 6: Create `IJwtTokenService` interface**

Create `src/Services/Identity/FairBank.Identity.Application/Auth/IJwtTokenService.cs`:

```csharp
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Application.Auth.DTOs;

namespace FairBank.Identity.Application.Auth;

public interface IJwtTokenService
{
    LoginResponse GenerateTokens(User user);
}
```

**Step 7: Create `JwtTokenService` implementation**

Create `src/Services/Identity/FairBank.Identity.Application/Auth/JwtTokenService.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FairBank.Identity.Application.Auth.DTOs;
using FairBank.Identity.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FairBank.Identity.Application.Auth;

public sealed class JwtTokenService(IOptions<JwtSettings> settings) : IJwtTokenService
{
    private readonly JwtSettings _settings = settings.Value;

    public LoginResponse GenerateTokens(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new("role", user.Role.ToString()),
            new("firstName", user.FirstName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.ParentId.HasValue)
            claims.Add(new Claim("parentId", user.ParentId.Value.ToString()));

        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Refresh token is a random string, stored in DB by the handler
        var refreshToken = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));

        return new LoginResponse(accessToken, refreshToken, expires);
    }
}
```

**Step 8: Add JWT packages to Identity Application csproj**

Modify `src/Services/Identity/FairBank.Identity.Application/FairBank.Identity.Application.csproj` — add to ItemGroup:

```xml
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" />
    <PackageReference Include="Microsoft.Extensions.Options" />
```

Note: `Microsoft.Extensions.Options` is likely already transitive, but add explicitly if build fails.

**Step 9: Write unit tests for RefreshToken**

Create `tests/FairBank.Identity.UnitTests/Domain/RefreshTokenTests.cs`:

```csharp
using FluentAssertions;
using FairBank.Identity.Domain.Entities;

namespace FairBank.Identity.UnitTests.Domain;

public class RefreshTokenTests
{
    [Fact]
    public void Create_ShouldGenerateValidToken()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), TimeSpan.FromDays(7));

        token.Id.Should().NotBeEmpty();
        token.Token.Should().NotBeNullOrWhiteSpace();
        token.IsRevoked.Should().BeFalse();
        token.IsValid.Should().BeTrue();
        token.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void Revoke_ShouldInvalidateToken()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), TimeSpan.FromDays(7));
        token.Revoke();

        token.IsRevoked.Should().BeTrue();
        token.IsValid.Should().BeFalse();
    }
}
```

**Step 10: Run tests**

Run: `dotnet test FairBank.slnx`
Expected: 42 tests pass (40 existing + 2 new)

**Step 11: Commit**

```bash
git add -A && git commit -m "feat(identity): add RefreshToken entity, JWT token service, and ParentId on User"
```

---

### Task 4: Login, Refresh, Logout Commands + Auth Endpoints

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/Login/LoginCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/Login/LoginCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/Login/LoginCommandValidator.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/RefreshToken/RefreshTokenCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/Logout/LogoutCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/Logout/LogoutCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Program.cs` — add auth endpoints, JWT config
- Modify: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandHandler.cs` — switch to BCrypt
- Modify: `src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj` — add BCrypt package
- Test: `tests/FairBank.Identity.UnitTests/Application/LoginCommandHandlerTests.cs`

**Step 1: Add BCrypt package to Identity Api csproj**

Add to `src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj`:

```xml
    <PackageReference Include="BCrypt.Net-Next" />
```

Also add to `src/Services/Identity/FairBank.Identity.Application/FairBank.Identity.Application.csproj`:

```xml
    <PackageReference Include="BCrypt.Net-Next" />
```

**Step 2: Update `RegisterUserCommandHandler` to use BCrypt**

In `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandHandler.cs`, replace the SHA256 hashing (lines 22-25) with:

```csharp
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
```

Remove the SHA256 comment and old code.

**Step 3: Create `LoginCommand`**

Create `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/Login/LoginCommand.cs`:

```csharp
using FairBank.Identity.Application.Auth.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;
```

**Step 4: Create `LoginCommandValidator`**

Create `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/Login/LoginCommandValidator.cs`:

```csharp
using FluentValidation;

namespace FairBank.Identity.Application.Auth.Commands.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
```

**Step 5: Create `LoginCommandHandler`**

Create `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/Login/LoginCommandHandler.cs`:

```csharp
using FairBank.Identity.Application.Auth.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var email = Email.Create(request.Email);
        var user = await userRepository.GetByEmailAsync(email, ct)
            ?? throw new InvalidOperationException("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new InvalidOperationException("Invalid email or password.");

        if (!user.IsActive)
            throw new InvalidOperationException("Account is deactivated.");

        // Revoke all existing refresh tokens for this user (single session)
        var existingTokens = await refreshTokenRepository.GetActiveByUserIdAsync(user.Id, ct);
        foreach (var existing in existingTokens)
        {
            existing.Revoke();
            await refreshTokenRepository.UpdateAsync(existing, ct);
        }

        // Generate new tokens
        var loginResponse = jwtTokenService.GenerateTokens(user);

        // Store refresh token in DB
        var refreshToken = RefreshToken.Create(user.Id, TimeSpan.FromDays(7));
        await refreshTokenRepository.AddAsync(refreshToken, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new LoginResponse(loginResponse.AccessToken, refreshToken.Token, loginResponse.ExpiresAt);
    }
}
```

**Step 6: Create `RefreshTokenCommand` + handler**

Create `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/RefreshToken/RefreshTokenCommand.cs`:

```csharp
using FairBank.Identity.Application.Auth.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResponse>;
```

Create `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs`:

```csharp
using FairBank.Identity.Application.Auth.DTOs;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RefreshTokenCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var storedToken = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct)
            ?? throw new InvalidOperationException("Invalid refresh token.");

        if (!storedToken.IsValid)
            throw new InvalidOperationException("Refresh token is expired or revoked.");

        var user = await userRepository.GetByIdAsync(storedToken.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        // Revoke used token
        storedToken.Revoke();
        await refreshTokenRepository.UpdateAsync(storedToken, ct);

        // Generate new tokens
        var loginResponse = jwtTokenService.GenerateTokens(user);

        var newRefreshToken = Domain.Entities.RefreshToken.Create(user.Id, TimeSpan.FromDays(7));
        await refreshTokenRepository.AddAsync(newRefreshToken, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new LoginResponse(loginResponse.AccessToken, newRefreshToken.Token, loginResponse.ExpiresAt);
    }
}
```

**Step 7: Create `LogoutCommand` + handler**

Create `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/Logout/LogoutCommand.cs`:

```csharp
using MediatR;

namespace FairBank.Identity.Application.Auth.Commands.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest;
```

Create `src/Services/Identity/FairBank.Identity.Application/Auth/Commands/Logout/LogoutCommandHandler.cs`:

```csharp
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Auth.Commands.Logout;

public sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        var token = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (token is null) return;

        token.Revoke();
        await refreshTokenRepository.UpdateAsync(token, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
```

**Step 8: Create `AuthEndpoints`**

Create `src/Services/Identity/FairBank.Identity.Api/Endpoints/AuthEndpoints.cs`:

```csharp
using FairBank.Identity.Application.Auth.Commands.Login;
using FairBank.Identity.Application.Auth.Commands.Logout;
using FairBank.Identity.Application.Auth.Commands.RefreshToken;
using FairBank.Identity.Application.Users.Queries.GetUserById;
using MediatR;
using System.Security.Claims;

namespace FairBank.Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth");

        group.MapPost("/login", async (LoginCommand command, ISender sender) =>
        {
            try
            {
                var result = await sender.Send(command);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("Login")
        .AllowAnonymous();

        group.MapPost("/refresh", async (RefreshTokenCommand command, ISender sender) =>
        {
            try
            {
                var result = await sender.Send(command);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Unauthorized();
            }
        })
        .WithName("RefreshToken")
        .AllowAnonymous();

        group.MapPost("/logout", async (LogoutCommand command, ISender sender) =>
        {
            await sender.Send(command);
            return Results.NoContent();
        })
        .WithName("Logout");

        return group;
    }

    public static RouteGroupBuilder MapMeEndpoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("Users");

        group.MapGet("/me", async (ClaimsPrincipal user, ISender sender) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("User ID not found in token."));

            var result = await sender.Send(new GetUserByIdQuery(userId));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetCurrentUser")
        .RequireAuthorization();

        return group;
    }
}
```

**Step 9: Update Identity API Program.cs**

Modify `src/Services/Identity/FairBank.Identity.Api/Program.cs` — replace entire file:

```csharp
using FairBank.Identity.Api.Endpoints;
using FairBank.Identity.Application;
using FairBank.Identity.Application.Auth;
using FairBank.Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

// JWT settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// Application layer (MediatR, FluentValidation)
builder.Services.AddIdentityApplication();

// Infrastructure layer (EF Core, repositories)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
builder.Services.AddIdentityInfrastructure(connectionString);

// JWT token service
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapMeEndpoint();

// Health check
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Identity" }))
    .WithTags("Health");

app.Run();

// Required for integration tests
public partial class Program;
```

**Step 10: Add JWT config to appsettings.Development.json**

Modify `src/Services/Identity/FairBank.Identity.Api/appsettings.Development.json` — add Jwt section:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=identity_service"
  },
  "Jwt": {
    "Secret": "FairBank-Super-Secret-Key-For-JWT-2026-Must-Be-At-Least-32-Chars!",
    "Issuer": "fairbank-identity",
    "Audience": "fairbank-api",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

Also add to `src/Services/Identity/FairBank.Identity.Api/appsettings.json`:

```json
{
  "Jwt": {
    "Secret": "CHANGE-THIS-IN-PRODUCTION-MUST-BE-AT-LEAST-32-CHARACTERS-LONG!!",
    "Issuer": "fairbank-identity",
    "Audience": "fairbank-api",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

**Step 11: Add JwtBearer package to Identity API csproj**

Add to `src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj`:

```xml
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
```

**Step 12: Run build**

Run: `dotnet build FairBank.slnx`
Expected: Build succeeded

**Step 13: Commit**

```bash
git add -A && git commit -m "feat(identity): add JWT login, refresh, logout endpoints with BCrypt password hashing"
```

---

### Task 5: Identity Infrastructure — RefreshToken Repository + EF Config + Migration

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/IdentityDbContext.cs` — add RefreshTokens DbSet
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs` — add ParentId FK
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/DependencyInjection.cs` — register RefreshTokenRepository

**Step 1: Add `RefreshTokens` to DbContext**

In `IdentityDbContext.cs`, add after line 10:

```csharp
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
```

Add using: `using FairBank.Identity.Domain.Entities;` (already present for User).

**Step 2: Create `RefreshTokenConfiguration`**

Create `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`:

```csharp
using FairBank.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Token).HasMaxLength(500).IsRequired();
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.IsRevoked).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => t.UserId);
    }
}
```

**Step 3: Update `UserConfiguration` for ParentId**

In `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs`, add before the query filter (before line 48):

```csharp
        // Parent-child self-reference
        builder.Property(u => u.ParentId);

        builder.HasOne(u => u.Parent)
            .WithMany(u => u.Children)
            .HasForeignKey(u => u.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(u => u.ParentId);
```

Note: The `Children` navigation property accesses a private `_children` field. EF Core can access this via the backing field convention. You may need to add to User.cs: change `private readonly List<User> _children = [];` to have EF Core use it as backing field. Add this line in UserConfiguration:

```csharp
        builder.Navigation(u => u.Children).HasField("_children");
```

**Step 4: Create `RefreshTokenRepository`**

Create `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs`:

```csharp
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Identity.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(IdentityDbContext db) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token, ct);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(ct);
    }

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default)
    {
        await db.RefreshTokens.AddAsync(refreshToken, ct);
    }

    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct = default)
    {
        db.RefreshTokens.Update(refreshToken);
        await Task.CompletedTask;
    }
}
```

**Step 5: Register in DI**

In `src/Services/Identity/FairBank.Identity.Infrastructure/DependencyInjection.cs`, add after the `IUserRepository` registration:

```csharp
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
```

Add using: `using FairBank.Identity.Domain.Ports;` (already present).

**Step 6: Add `GetChildrenAsync` to `IUserRepository` and `UserRepository`**

In `IUserRepository.cs`, add:

```csharp
    Task<IReadOnlyList<User>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);
```

In `UserRepository.cs` (find at `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs`), add:

```csharp
    public async Task<IReadOnlyList<User>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
    {
        return await db.Users.Where(u => u.ParentId == parentId).ToListAsync(ct);
    }
```

**Step 7: Generate EF Core migration**

Run:
```bash
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add AddRefreshTokensAndParentChild \
  --project src/Services/Identity/FairBank.Identity.Infrastructure \
  --startup-project src/Services/Identity/FairBank.Identity.Api
```

Expected: Migration file created in `Persistence/Migrations/`

**Step 8: Run build + tests**

Run: `dotnet build FairBank.slnx && dotnet test FairBank.slnx`
Expected: Build succeeded, all tests pass

**Step 9: Commit**

```bash
git add -A && git commit -m "feat(identity): add RefreshToken repository, ParentId FK on User, EF Core migration"
```

---

### Task 6: API Gateway — JWT Validation + RBAC + Rate Limiting + CORS

**Files:**
- Modify: `src/FairBank.ApiGateway/Program.cs` — add JWT, RBAC, rate limiting, CORS
- Modify: `src/FairBank.ApiGateway/FairBank.ApiGateway.csproj` — add JWT package
- Modify: `src/FairBank.ApiGateway/appsettings.json` — add JWT config, auth route
- Create: `src/FairBank.ApiGateway/appsettings.Development.json` — dev JWT secret

**Step 1: Add packages to Gateway csproj**

Modify `src/FairBank.ApiGateway/FairBank.ApiGateway.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <PackageReference Include="Yarp.ReverseProxy" />
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Sinks.Console" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
  </ItemGroup>
</Project>
```

**Step 2: Update Gateway Program.cs**

Replace `src/FairBank.ApiGateway/Program.cs`:

```csharp
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT secret is missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "fairbank-identity",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "fairbank-api",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

// Authorization policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ClientOnly", p => p.RequireRole("Client", "Admin"))
    .AddPolicy("BankerOnly", p => p.RequireRole("Banker", "Admin"))
    .AddPolicy("AdminOnly", p => p.RequireRole("Admin"));

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("global", cfg =>
    {
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.PermitLimit = 100;
        cfg.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("auth", cfg =>
    {
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.PermitLimit = 5;
        cfg.QueueLimit = 0;
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("FairBankPolicy", policy =>
    {
        policy.WithOrigins("http://localhost", "https://localhost")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors("FairBankPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ApiGateway" }));

app.Run();
```

**Step 3: Update Gateway appsettings.json**

Add JWT config and auth route to `src/FairBank.ApiGateway/appsettings.json`. Add the `auth-route` to the Routes section:

```json
        "auth-route": {
          "ClusterId": "identity-cluster",
          "Match": {
            "Path": "/api/v1/auth/{**catch-all}"
          }
        },
```

Add JWT section at root level:

```json
  "Jwt": {
    "Secret": "FairBank-Super-Secret-Key-For-JWT-2026-Must-Be-At-Least-32-Chars!",
    "Issuer": "fairbank-identity",
    "Audience": "fairbank-api"
  }
```

**Step 4: Add JWT config to docker-compose environment**

In `docker-compose.yml`, add to `api-gateway` service environment:

```yaml
      environment:
        ASPNETCORE_ENVIRONMENT: Development
        Jwt__Secret: "FairBank-Super-Secret-Key-For-JWT-2026-Must-Be-At-Least-32-Chars!"
        Jwt__Issuer: "fairbank-identity"
        Jwt__Audience: "fairbank-api"
```

Also add Jwt env vars to `identity-api` service:

```yaml
        Jwt__Secret: "FairBank-Super-Secret-Key-For-JWT-2026-Must-Be-At-Least-32-Chars!"
        Jwt__Issuer: "fairbank-identity"
        Jwt__Audience: "fairbank-api"
```

**Step 5: Run build**

Run: `dotnet build FairBank.slnx`
Expected: Build succeeded

**Step 6: Commit**

```bash
git add -A && git commit -m "feat(gateway): add JWT validation, RBAC policies, rate limiting, and CORS"
```

---

### Task 7: Audit Log Entity + Middleware

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Domain/Entities/AuditLog.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/IdentityDbContext.cs` — add AuditLogs DbSet
- Create: `src/Services/Identity/FairBank.Identity.Domain/Ports/IAuditLogRepository.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/AuditLogRepository.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/DependencyInjection.cs` — register repository

**Step 1: Create `AuditLog` entity**

Create `src/Services/Identity/FairBank.Identity.Domain/Entities/AuditLog.cs`:

```csharp
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Entities;

public sealed class AuditLog : Entity<Guid>
{
    public string Action { get; private set; } = null!;
    public Guid? UserId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? Details { get; private set; }
    public DateTime Timestamp { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(string action, Guid? userId = null, string? ipAddress = null, string? details = null)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            UserId = userId,
            IpAddress = ipAddress,
            Details = details,
            Timestamp = DateTime.UtcNow
        };
    }
}
```

**Step 2: Create `IAuditLogRepository`**

Create `src/Services/Identity/FairBank.Identity.Domain/Ports/IAuditLogRepository.cs`:

```csharp
using FairBank.Identity.Domain.Entities;

namespace FairBank.Identity.Domain.Ports;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken ct = default);
}
```

**Step 3: Create EF Core configuration**

Create `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs`:

```csharp
using FairBank.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Identity.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(50);
        builder.Property(a => a.Details).HasMaxLength(4000);
        builder.Property(a => a.Timestamp).IsRequired();
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.Timestamp);
    }
}
```

**Step 4: Add DbSet to context**

In `IdentityDbContext.cs`, add:

```csharp
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
```

**Step 5: Create repository + register in DI**

Create `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/AuditLogRepository.cs`:

```csharp
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;

namespace FairBank.Identity.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(IdentityDbContext db) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog auditLog, CancellationToken ct = default)
    {
        await db.AuditLogs.AddAsync(auditLog, ct);
        await db.SaveChangesAsync(ct);
    }
}
```

Register in DI (`DependencyInjection.cs`):

```csharp
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
```

**Step 6: Generate migration**

Run:
```bash
dotnet ef migrations add AddAuditLogsTable \
  --project src/Services/Identity/FairBank.Identity.Infrastructure \
  --startup-project src/Services/Identity/FairBank.Identity.Api
```

**Step 7: Build + test**

Run: `dotnet build FairBank.slnx && dotnet test FairBank.slnx`

**Step 8: Commit**

```bash
git add -A && git commit -m "feat(identity): add AuditLog entity with EF Core configuration and migration"
```

---

### Task 8: PostgreSQL Primary + Replica Docker Setup

**Files:**
- Create: `docker/postgres/primary-init.sh`
- Create: `docker/postgres/replica-entrypoint.sh`
- Modify: `docker-compose.yml` — replace single postgres with primary + replica
- Modify: `docker/postgres/init.sql` — add replication grant

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

Make executable: `chmod +x docker/postgres/primary-init.sh`

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

  # Ensure standby.signal exists (pg_basebackup -R creates it, but just in case)
  touch /var/lib/postgresql/data/standby.signal

  echo "Base backup complete. Starting replica..."
fi

# Start PostgreSQL
exec postgres \
  -c hot_standby=on \
  -c shared_buffers=64MB
```

Make executable: `chmod +x docker/postgres/replica-entrypoint.sh`

**Step 3: Update `docker-compose.yml`**

Replace the single `postgres` service with `postgres-primary` and `postgres-replica`. Update connection strings in `identity-api` and `accounts-api` to point to `postgres-primary`:

Replace `postgres` service block with:

```yaml
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
```

Update `depends_on` in both API services from `postgres` to `postgres-primary`.

Update connection strings to use `Host=postgres-primary`.

Add to volumes section:

```yaml
  pgdata-primary:
  pgdata-replica:
```

Remove old `pgdata:` volume.

**Step 4: Verify Docker build**

Note: Docker build must be done by the user (permission issue). But verify the compose file is valid:

Run: `docker compose config --quiet`
Expected: No errors

**Step 5: Commit**

```bash
chmod +x docker/postgres/primary-init.sh docker/postgres/replica-entrypoint.sh
git add -A && git commit -m "feat(infra): add PostgreSQL primary-replica streaming replication"
```

---

### Task 9: Child Accounts — Identity Endpoints

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/CreateChild/CreateChildCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/CreateChild/CreateChildCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/CreateChild/CreateChildCommandValidator.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetChildren/GetChildrenQuery.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetChildren/GetChildrenQueryHandler.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs` — add child endpoints
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

In `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`, add inside the `MapUserEndpoints` method, after existing endpoints:

```csharp
        group.MapPost("/{parentId:guid}/children", async (Guid parentId, CreateChildCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { ParentId = parentId });
            return Results.Created($"/api/v1/users/{result.Id}", result);
        })
        .WithName("CreateChild")
        .RequireAuthorization()
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{parentId:guid}/children", async (Guid parentId, ISender sender) =>
        {
            var result = await sender.Send(new GetChildrenQuery(parentId));
            return Results.Ok(result);
        })
        .WithName("GetChildren")
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK);
```

Add using for the new commands/queries:
```csharp
using FairBank.Identity.Application.Users.Commands.CreateChild;
using FairBank.Identity.Application.Users.Queries.GetChildren;
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
Expected: All tests pass (42 existing + 2 new = 44)

**Step 8: Commit**

```bash
git add -A && git commit -m "feat(identity): add child account creation and listing endpoints"
```

---

### Task 10: Accounts — Spending Limits + PendingTransaction

**Files:**
- Modify: `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/Account.cs` — add SpendingLimit, RequiresApproval, ApprovalThreshold
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/PendingTransaction.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Enums/PendingTransactionStatus.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/SpendingLimitSet.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/TransactionRequested.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/TransactionApproved.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/TransactionRejected.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/SetSpendingLimit/`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/ApproveTransaction/`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/RejectTransaction/`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetAccountsByOwner/`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetPendingTransactions/`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Ports/IPendingTransactionStore.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/DTOs/PendingTransactionResponse.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/MartenPendingTransactionStore.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Infrastructure/DependencyInjection.cs` — register PendingTransaction projection
- Modify: `src/Services/Accounts/FairBank.Accounts.Api/Endpoints/AccountEndpoints.cs` — add new endpoints
- Test: `tests/FairBank.Accounts.UnitTests/Domain/PendingTransactionTests.cs`

**Step 1: Create domain events**

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

**Step 3: Add spending limit fields to Account**

In `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/Account.cs`, add after `CreatedAt` property:

```csharp
    public Money? SpendingLimit { get; private set; }
    public bool RequiresApproval { get; private set; }
    public Money? ApprovalThreshold { get; private set; }
```

Add method:

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

Add Apply method:

```csharp
    public void Apply(SpendingLimitSet @event)
    {
        SpendingLimit = Money.Create(@event.Limit, @event.Currency);
        RequiresApproval = true;
        ApprovalThreshold = SpendingLimit;
    }
```

**Step 4: Create `PendingTransaction` aggregate**

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

**Step 5: Create `IPendingTransactionStore`**

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

**Step 6: Create `PendingTransactionResponse` DTO**

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

**Step 7: Create commands and queries**

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
using FairBank.Accounts.Domain.ValueObjects;
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

Create `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetAccountsByOwner/GetAccountsByOwnerQuery.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountsByOwner;

public sealed record GetAccountsByOwnerQuery(Guid OwnerId) : IRequest<IReadOnlyList<AccountResponse>>;
```

Create `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetAccountsByOwner/GetAccountsByOwnerQueryHandler.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountsByOwner;

public sealed class GetAccountsByOwnerQueryHandler(IAccountEventStore eventStore)
    : IRequestHandler<GetAccountsByOwnerQuery, IReadOnlyList<AccountResponse>>
{
    public async Task<IReadOnlyList<AccountResponse>> Handle(GetAccountsByOwnerQuery request, CancellationToken ct)
    {
        var accounts = await eventStore.GetByOwnerIdAsync(request.OwnerId, ct);
        return accounts.Select(a => new AccountResponse(
            a.Id, a.OwnerId, a.AccountNumber.Value,
            a.Balance.Amount, a.Balance.Currency,
            a.IsActive, a.CreatedAt)).ToList();
    }
}
```

Note: This requires adding `GetByOwnerIdAsync` to `IAccountEventStore`:

```csharp
    Task<IReadOnlyList<Account>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);
```

**Step 8: Implement `MartenPendingTransactionStore` and update `MartenAccountEventStore`**

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

Add `GetByOwnerIdAsync` to `MartenAccountEventStore`:

```csharp
    public async Task<IReadOnlyList<Account>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
    {
        return await session.Query<Account>()
            .Where(a => a.OwnerId == ownerId)
            .ToListAsync(ct);
    }
```

**Step 9: Register in DI and add Marten projections**

In `src/Services/Accounts/FairBank.Accounts.Infrastructure/DependencyInjection.cs`, add `PendingTransaction` snapshot registration:

```csharp
    options.Projections.Snapshot<PendingTransaction>(SnapshotLifecycle.Inline);
```

Register store:

```csharp
    services.AddScoped<IPendingTransactionStore, MartenPendingTransactionStore>();
```

**Step 10: Add new endpoints**

In `src/Services/Accounts/FairBank.Accounts.Api/Endpoints/AccountEndpoints.cs`, add new endpoints inside the `MapAccountEndpoints` method:

```csharp
        // List accounts by owner
        group.MapGet("/", async ([AsParameters] Guid? ownerId, ISender sender) =>
        {
            if (ownerId is null) return Results.BadRequest("ownerId query parameter is required.");
            var result = await sender.Send(new GetAccountsByOwnerQuery(ownerId.Value));
            return Results.Ok(result);
        })
        .WithName("GetAccountsByOwner")
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK);

        // Set spending limit
        group.MapPost("/{id:guid}/limits", async (Guid id, SetSpendingLimitCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = id });
            return Results.Ok(result);
        })
        .WithName("SetSpendingLimit")
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK);

        // Pending transactions
        group.MapGet("/{id:guid}/pending", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPendingTransactionsQuery(id));
            return Results.Ok(result);
        })
        .WithName("GetPendingTransactions")
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK);

        // Approve/Reject
        var pendingGroup = app.MapGroup("/api/v1/accounts/pending")
            .WithTags("PendingTransactions");

        pendingGroup.MapPost("/{id:guid}/approve", async (Guid id, ApproveTransactionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { TransactionId = id });
            return Results.Ok(result);
        })
        .WithName("ApproveTransaction")
        .RequireAuthorization();

        pendingGroup.MapPost("/{id:guid}/reject", async (Guid id, RejectTransactionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { TransactionId = id });
            return Results.Ok(result);
        })
        .WithName("RejectTransaction")
        .RequireAuthorization();
```

Also need `GetPendingTransactionsQuery` — create `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetPendingTransactions/GetPendingTransactionsQuery.cs`:

```csharp
using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetPendingTransactions;

public sealed record GetPendingTransactionsQuery(Guid AccountId) : IRequest<IReadOnlyList<PendingTransactionResponse>>;
```

Create handler `GetPendingTransactionsQueryHandler.cs`:

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

**Step 11: Write tests for PendingTransaction**

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
        tx.Reject(approverId, "Příliš drahé");

        tx.Status.Should().Be(PendingTransactionStatus.Rejected);
        tx.RejectionReason.Should().Be("Příliš drahé");
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

**Step 12: Run build + tests**

Run: `dotnet build FairBank.slnx && dotnet test FairBank.slnx`
Expected: All tests pass

**Step 13: Commit**

```bash
git add -A && git commit -m "feat(accounts): add spending limits, PendingTransaction aggregate, and approval endpoints"
```

---

### Task 11: Frontend — Auth Integration

**Files:**
- Modify: `src/FairBank.Web.Shared/Services/IFairBankApi.cs` — add auth + child + pending methods
- Modify: `src/FairBank.Web.Shared/Services/FairBankApiClient.cs` — implement new methods
- Create: `src/FairBank.Web.Shared/Models/LoginRequest.cs`
- Create: `src/FairBank.Web.Shared/Models/LoginResponse.cs`
- Create: `src/FairBank.Web.Shared/Models/PendingTransactionDto.cs`
- Modify: `src/FairBank.Web/Program.cs` — add auth services
- Modify: `src/FairBank.Web/FairBank.Web.csproj` — add auth package

**Step 1: Add auth models**

Create `src/FairBank.Web.Shared/Models/LoginRequest.cs`:

```csharp
namespace FairBank.Web.Shared.Models;

public sealed record LoginRequest(string Email, string Password);
```

Create `src/FairBank.Web.Shared/Models/LoginResponse.cs`:

```csharp
namespace FairBank.Web.Shared.Models;

public sealed record LoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
```

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

Add to `src/FairBank.Web.Shared/Services/IFairBankApi.cs`:

```csharp
    // Auth
    Task<LoginResponse?> LoginAsync(string email, string password);
    Task<LoginResponse?> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);

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

Add implementations to `src/FairBank.Web.Shared/Services/FairBankApiClient.cs`:

```csharp
    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }

    public async Task<LoginResponse?> RefreshTokenAsync(string refreshToken)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/auth/refresh", new { RefreshToken = refreshToken });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }

    public async Task LogoutAsync(string refreshToken)
    {
        await _http.PostAsJsonAsync("/api/v1/auth/logout", new { RefreshToken = refreshToken });
    }

    public async Task<List<UserResponse>> GetChildrenAsync(Guid parentId)
    {
        return await _http.GetFromJsonAsync<List<UserResponse>>($"/api/v1/users/{parentId}/children") ?? [];
    }

    public async Task<UserResponse> CreateChildAsync(Guid parentId, string firstName, string lastName, string email, string password)
    {
        var response = await _http.PostAsJsonAsync($"/api/v1/users/{parentId}/children",
            new { FirstName = firstName, LastName = lastName, Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    public async Task<List<AccountResponse>> GetAccountsByOwnerAsync(Guid ownerId)
    {
        return await _http.GetFromJsonAsync<List<AccountResponse>>($"/api/v1/accounts?ownerId={ownerId}") ?? [];
    }

    public async Task<List<PendingTransactionDto>> GetPendingTransactionsAsync(Guid accountId)
    {
        return await _http.GetFromJsonAsync<List<PendingTransactionDto>>($"/api/v1/accounts/{accountId}/pending") ?? [];
    }

    public async Task<PendingTransactionDto> ApproveTransactionAsync(Guid transactionId, Guid approverId)
    {
        var response = await _http.PostAsJsonAsync($"/api/v1/accounts/pending/{transactionId}/approve",
            new { ApproverId = approverId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PendingTransactionDto>())!;
    }

    public async Task<PendingTransactionDto> RejectTransactionAsync(Guid transactionId, Guid approverId, string reason)
    {
        var response = await _http.PostAsJsonAsync($"/api/v1/accounts/pending/{transactionId}/reject",
            new { ApproverId = approverId, Reason = reason });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PendingTransactionDto>())!;
    }
```

**Step 4: Add auth package to Web project**

Add to `src/FairBank.Web/FairBank.Web.csproj`:

```xml
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Authentication" />
```

Add to `Directory.Packages.props`:

```xml
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly.Authentication" Version="10.0.3" />
```

**Step 5: Build**

Run: `dotnet build FairBank.slnx`
Expected: Build succeeded

**Step 6: Commit**

```bash
git add -A && git commit -m "feat(frontend): extend IFairBankApi with auth, children, and pending transaction methods"
```

---

### Task 12: Final — Run All Tests + Docker Config Validation

**Step 1: Run full test suite**

Run: `dotnet test FairBank.slnx -v minimal`
Expected: All tests pass (44+ tests)

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
