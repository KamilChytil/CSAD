# FairBank Remaining Features Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement the 3 remaining features: Two-Factor Authentication (TOTP), Device Management, and Financial Limits Enforcement — plus frontend integration for all new backend capabilities.

**Architecture:** Extend the existing Identity microservice (EF Core, PostgreSQL) with new domain entities and commands for 2FA and devices. Enhance the Payments microservice to enforce account limits during payment processing. Build Blazor WASM UI components in the Web.Profile module.

**Tech Stack:** .NET 10, EF Core, MediatR, FluentValidation, BCrypt, TOTP (RFC 6238), Blazor WASM, SignalR

---

## Current State Assessment

After thorough codebase analysis, the following tasks are **already complete**:
- Registration with KYC data persistence ✅
- Email verification, password reset/change ✅
- Account management (close, rename, multi-account) ✅
- Payment Cards microservice (full CRUD, PIN, limits) ✅
- Notifications microservice (full CRUD, SignalR hub, preferences) ✅
- Transaction statistics, filtering, categories, export ✅
- QR Payments (SPAYD generation/parsing) ✅
- Chat improvements (typing, read receipts, attachments, search) ✅
- Docker infrastructure for all services ✅
- API Gateway routes for all services ✅

**Remaining work:**
1. Task 3: Two-Factor Authentication (TOTP)
2. Task 4: Device Management
3. Task 7: Financial & Security Limits Enforcement
4. Task 12: Frontend Integration & Polish

---

### Task 1: Two-Factor Authentication (TOTP) — Domain Layer

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Domain/Entities/TwoFactorAuth.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Domain/Ports/IUserRepository.cs`

**Step 1: Add TwoFactorAuth entity**

Create `src/Services/Identity/FairBank.Identity.Domain/Entities/TwoFactorAuth.cs`:

```csharp
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Entities;

public sealed class TwoFactorAuth : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string SecretKey { get; private set; } = null!;
    public bool IsEnabled { get; private set; }
    public string? BackupCodes { get; private set; } // JSON array of hashed codes
    public DateTime CreatedAt { get; private set; }
    public DateTime? EnabledAt { get; private set; }

    private TwoFactorAuth() { } // EF Core

    public static TwoFactorAuth Create(Guid userId, string secretKey)
    {
        return new TwoFactorAuth
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SecretKey = secretKey,
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Enable(string hashedBackupCodes)
    {
        if (IsEnabled)
            throw new InvalidOperationException("2FA is already enabled.");

        IsEnabled = true;
        EnabledAt = DateTime.UtcNow;
        BackupCodes = hashedBackupCodes;
    }

    public void Disable()
    {
        if (!IsEnabled)
            throw new InvalidOperationException("2FA is not enabled.");

        IsEnabled = false;
        EnabledAt = null;
        BackupCodes = null;
    }

    public void RegenerateBackupCodes(string hashedBackupCodes)
    {
        if (!IsEnabled)
            throw new InvalidOperationException("2FA must be enabled to regenerate backup codes.");

        BackupCodes = hashedBackupCodes;
    }
}
```

**Step 2: Add 2FA fields to User aggregate**

Add to `User.cs`:

```csharp
public bool IsTwoFactorEnabled { get; private set; }

public void EnableTwoFactor() => IsTwoFactorEnabled = true;
public void DisableTwoFactor() => IsTwoFactorEnabled = false;
```

**Step 3: Add TwoFactorAuth repository port**

Create `src/Services/Identity/FairBank.Identity.Domain/Ports/ITwoFactorAuthRepository.cs`:

```csharp
namespace FairBank.Identity.Domain.Ports;

public interface ITwoFactorAuthRepository
{
    Task<TwoFactorAuth?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(TwoFactorAuth twoFactorAuth, CancellationToken ct = default);
    Task UpdateAsync(TwoFactorAuth twoFactorAuth, CancellationToken ct = default);
    Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default);
}
```

**Step 4: Commit**

```bash
git add src/Services/Identity/FairBank.Identity.Domain/
git commit -m "feat(identity): add TwoFactorAuth domain entity and repository port"
```

---

### Task 2: Two-Factor Authentication (TOTP) — Application Layer

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/SetupTwoFactor/SetupTwoFactorCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/SetupTwoFactor/SetupTwoFactorCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/EnableTwoFactor/EnableTwoFactorCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/EnableTwoFactor/EnableTwoFactorCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/DisableTwoFactor/DisableTwoFactorCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/DisableTwoFactor/DisableTwoFactorCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/VerifyTwoFactor/VerifyTwoFactorCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/VerifyTwoFactor/VerifyTwoFactorCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/TwoFactorSetupResponse.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Helpers/TotpHelper.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/LoginUser/LoginUserCommandHandler.cs`

**Step 1: Create TOTP helper**

Create `src/Services/Identity/FairBank.Identity.Application/Helpers/TotpHelper.cs`:

```csharp
using System.Security.Cryptography;

namespace FairBank.Identity.Application.Helpers;

public static class TotpHelper
{
    private const int SecretLength = 20;
    private const int CodeDigits = 6;
    private const int TimeStepSeconds = 30;
    private const int AllowedDrift = 1; // ±1 time step

    public static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretLength);
        return Base32Encode(bytes);
    }

    public static bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != CodeDigits)
            return false;

        var secretBytes = Base32Decode(secret);
        var timeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TimeStepSeconds;

        for (var i = -AllowedDrift; i <= AllowedDrift; i++)
        {
            var expectedCode = GenerateCode(secretBytes, timeStep + i);
            if (expectedCode == code)
                return true;
        }

        return false;
    }

    public static string GetOtpAuthUri(string secret, string email, string issuer = "FairBank")
    {
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}" +
               $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits={CodeDigits}&period={TimeStepSeconds}";
    }

    public static string[] GenerateBackupCodes(int count = 8)
    {
        var codes = new string[count];
        for (var i = 0; i < count; i++)
        {
            codes[i] = $"{RandomNumberGenerator.GetInt32(10000000, 99999999)}";
        }
        return codes;
    }

    private static string GenerateCode(byte[] secret, long timeStep)
    {
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian) Array.Reverse(timeBytes);

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        return (binaryCode % (int)Math.Pow(10, CodeDigits)).ToString().PadLeft(CodeDigits, '0');
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new char[(data.Length * 8 + 4) / 5];
        var buffer = 0;
        var bitsLeft = 0;
        var index = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                result[index++] = alphabet[(buffer >> (bitsLeft - 5)) & 0x1F];
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
            result[index] = alphabet[(buffer << (5 - bitsLeft)) & 0x1F];

        return new string(result);
    }

    private static byte[] Base32Decode(string encoded)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in encoded.ToUpperInvariant())
        {
            var val = alphabet.IndexOf(c);
            if (val < 0) continue;
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }
}
```

**Step 2: Create TwoFactorSetupResponse DTO**

Create `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/TwoFactorSetupResponse.cs`:

```csharp
namespace FairBank.Identity.Application.Users.DTOs;

public sealed record TwoFactorSetupResponse(
    string Secret,
    string OtpAuthUri,
    bool IsAlreadyEnabled);
```

**Step 3: Create SetupTwoFactor command + handler**

`SetupTwoFactorCommand.cs`:
```csharp
using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.SetupTwoFactor;

public sealed record SetupTwoFactorCommand(Guid UserId) : IRequest<TwoFactorSetupResponse>;
```

`SetupTwoFactorCommandHandler.cs`:
```csharp
using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.SetupTwoFactor;

public sealed class SetupTwoFactorCommandHandler(
    IUserRepository userRepo,
    ITwoFactorAuthRepository tfaRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<SetupTwoFactorCommand, TwoFactorSetupResponse>
{
    public async Task<TwoFactorSetupResponse> Handle(SetupTwoFactorCommand request, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var existing = await tfaRepo.GetByUserIdAsync(request.UserId, ct);
        if (existing is not null && existing.IsEnabled)
        {
            return new TwoFactorSetupResponse(
                Secret: "",
                OtpAuthUri: "",
                IsAlreadyEnabled: true);
        }

        // Delete any existing non-enabled setup
        if (existing is not null)
            await tfaRepo.DeleteByUserIdAsync(request.UserId, ct);

        var secret = TotpHelper.GenerateSecret();
        var otpAuthUri = TotpHelper.GetOtpAuthUri(secret, user.Email.Value);

        var tfa = TwoFactorAuth.Create(request.UserId, secret);
        await tfaRepo.AddAsync(tfa, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new TwoFactorSetupResponse(
            Secret: secret,
            OtpAuthUri: otpAuthUri,
            IsAlreadyEnabled: false);
    }
}
```

**Step 4: Create EnableTwoFactor command + handler**

`EnableTwoFactorCommand.cs`:
```csharp
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.EnableTwoFactor;

public sealed record EnableTwoFactorCommand(Guid UserId, string Code) : IRequest<string[]>;
```

`EnableTwoFactorCommandHandler.cs`:
```csharp
using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.EnableTwoFactor;

public sealed class EnableTwoFactorCommandHandler(
    IUserRepository userRepo,
    ITwoFactorAuthRepository tfaRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<EnableTwoFactorCommand, string[]>
{
    public async Task<string[]> Handle(EnableTwoFactorCommand request, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var tfa = await tfaRepo.GetByUserIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("2FA setup not found. Call setup first.");

        if (tfa.IsEnabled)
            throw new InvalidOperationException("2FA is already enabled.");

        if (!TotpHelper.VerifyCode(tfa.SecretKey, request.Code))
            throw new InvalidOperationException("Invalid TOTP code.");

        // Generate backup codes
        var backupCodes = TotpHelper.GenerateBackupCodes();
        var hashedCodes = System.Text.Json.JsonSerializer.Serialize(
            backupCodes.Select(c => BCrypt.Net.BCrypt.HashPassword(c, workFactor: 10)).ToArray());

        tfa.Enable(hashedCodes);
        await tfaRepo.UpdateAsync(tfa, ct);

        user.EnableTwoFactor();
        await userRepo.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return backupCodes; // Return plaintext codes once, user must save them
    }
}
```

**Step 5: Create DisableTwoFactor command + handler**

`DisableTwoFactorCommand.cs`:
```csharp
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.DisableTwoFactor;

public sealed record DisableTwoFactorCommand(Guid UserId, string Code) : IRequest;
```

`DisableTwoFactorCommandHandler.cs`:
```csharp
using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.DisableTwoFactor;

public sealed class DisableTwoFactorCommandHandler(
    IUserRepository userRepo,
    ITwoFactorAuthRepository tfaRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DisableTwoFactorCommand>
{
    public async Task Handle(DisableTwoFactorCommand request, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var tfa = await tfaRepo.GetByUserIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("2FA is not set up.");

        if (!tfa.IsEnabled)
            throw new InvalidOperationException("2FA is not enabled.");

        // Verify with TOTP code or backup code
        if (!TotpHelper.VerifyCode(tfa.SecretKey, request.Code))
        {
            // Try backup codes
            if (!VerifyBackupCode(tfa, request.Code))
                throw new InvalidOperationException("Invalid code.");
        }

        tfa.Disable();
        await tfaRepo.UpdateAsync(tfa, ct);

        user.DisableTwoFactor();
        await userRepo.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static bool VerifyBackupCode(TwoFactorAuth tfa, string code)
    {
        if (string.IsNullOrEmpty(tfa.BackupCodes)) return false;

        var hashedCodes = System.Text.Json.JsonSerializer.Deserialize<string[]>(tfa.BackupCodes);
        return hashedCodes?.Any(h => BCrypt.Net.BCrypt.Verify(code, h)) == true;
    }
}
```

**Step 6: Create VerifyTwoFactor command (for login flow)**

`VerifyTwoFactorCommand.cs`:
```csharp
using FairBank.Identity.Application.Users.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.VerifyTwoFactor;

public sealed record VerifyTwoFactorCommand(
    Guid UserId,
    string Code) : IRequest<LoginResponse>;
```

`VerifyTwoFactorCommandHandler.cs`:
```csharp
using FairBank.Identity.Application.Helpers;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Users.Commands.VerifyTwoFactor;

public sealed class VerifyTwoFactorCommandHandler(
    IUserRepository userRepo,
    ITwoFactorAuthRepository tfaRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<VerifyTwoFactorCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(VerifyTwoFactorCommand request, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var tfa = await tfaRepo.GetByUserIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("2FA not configured.");

        var isValid = TotpHelper.VerifyCode(tfa.SecretKey, request.Code);

        // Try backup code if TOTP fails
        if (!isValid && !string.IsNullOrEmpty(tfa.BackupCodes))
        {
            var hashedCodes = System.Text.Json.JsonSerializer.Deserialize<string[]>(tfa.BackupCodes);
            if (hashedCodes is not null)
            {
                for (var i = 0; i < hashedCodes.Length; i++)
                {
                    if (BCrypt.Net.BCrypt.Verify(request.Code, hashedCodes[i]))
                    {
                        isValid = true;
                        // Invalidate used backup code
                        hashedCodes[i] = "";
                        tfa.RegenerateBackupCodes(
                            System.Text.Json.JsonSerializer.Serialize(hashedCodes));
                        await tfaRepo.UpdateAsync(tfa, ct);
                        break;
                    }
                }
            }
        }

        if (!isValid)
            throw new InvalidOperationException("Invalid 2FA code.");

        // Complete login - create session
        var sessionId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddHours(8);
        user.RecordSuccessfulLogin(sessionId, expiresAt);
        await userRepo.UpdateAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var token = SessionTokenHelper.Encode(user.Id, sessionId);

        return new LoginResponse(
            Token: token,
            UserId: user.Id,
            Email: user.Email.Value,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Role: user.Role.ToString(),
            SessionId: sessionId,
            ExpiresAt: expiresAt);
    }
}
```

**Step 7: Modify LoginUserCommandHandler to support 2FA**

In `LoginUserCommandHandler.cs`, after successful password verification, before creating session:

```csharp
// After BCrypt.Verify succeeds and email is verified:
// Check if 2FA is enabled
if (user.IsTwoFactorEnabled)
{
    // Return partial response indicating 2FA is required
    return new LoginResponse(
        Token: "",
        UserId: user.Id,
        Email: user.Email.Value,
        FirstName: user.FirstName,
        LastName: user.LastName,
        Role: user.Role.ToString(),
        SessionId: Guid.Empty,
        ExpiresAt: DateTime.MinValue)
    {
        RequiresTwoFactor = true
    };
}
```

Also add `RequiresTwoFactor` to `LoginResponse`:
```csharp
public sealed record LoginResponse(
    string Token,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    Guid SessionId,
    DateTime ExpiresAt)
{
    public bool RequiresTwoFactor { get; init; }
}
```

**Step 8: Commit**

```bash
git add src/Services/Identity/FairBank.Identity.Application/
git commit -m "feat(identity): add TOTP 2FA application layer with setup, enable, disable, verify commands"
```

---

### Task 3: Two-Factor Authentication — Infrastructure & API

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/TwoFactorAuthRepository.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/TwoFactorAuthConfiguration.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/IdentityDbContext.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/DependencyInjection.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`
- Create: EF Core migration

**Step 1: Create TwoFactorAuthConfiguration**

```csharp
using FairBank.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Identity.Infrastructure.Persistence.Configurations;

public sealed class TwoFactorAuthConfiguration : IEntityTypeConfiguration<TwoFactorAuth>
{
    public void Configure(EntityTypeBuilder<TwoFactorAuth> builder)
    {
        builder.ToTable("two_factor_auth");
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.UserId).IsUnique();
        builder.Property(t => t.SecretKey).HasMaxLength(200).IsRequired();
        builder.Property(t => t.BackupCodes).HasMaxLength(4000);
    }
}
```

**Step 2: Add DbSet to IdentityDbContext**

```csharp
public DbSet<TwoFactorAuth> TwoFactorAuths => Set<TwoFactorAuth>();
```

**Step 3: Create TwoFactorAuthRepository**

```csharp
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Identity.Infrastructure.Persistence.Repositories;

public sealed class TwoFactorAuthRepository(IdentityDbContext db) : ITwoFactorAuthRepository
{
    public async Task<TwoFactorAuth?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.TwoFactorAuths.FirstOrDefaultAsync(t => t.UserId == userId, ct);

    public async Task AddAsync(TwoFactorAuth twoFactorAuth, CancellationToken ct = default)
        => await db.TwoFactorAuths.AddAsync(twoFactorAuth, ct);

    public Task UpdateAsync(TwoFactorAuth twoFactorAuth, CancellationToken ct = default)
    {
        db.TwoFactorAuths.Update(twoFactorAuth);
        return Task.CompletedTask;
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        await db.TwoFactorAuths.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
    }
}
```

**Step 4: Register in DependencyInjection.cs**

Add: `services.AddScoped<ITwoFactorAuthRepository, TwoFactorAuthRepository>();`

**Step 5: Add User.IsTwoFactorEnabled to UserConfiguration**

In `UserConfiguration.cs` add:
```csharp
builder.Property(u => u.IsTwoFactorEnabled).HasDefaultValue(false);
```

**Step 6: Create migration**

Run: `dotnet ef migrations add AddTwoFactorAuth -p src/Services/Identity/FairBank.Identity.Infrastructure -s src/Services/Identity/FairBank.Identity.Api`

**Step 7: Add API endpoints in UserEndpoints.cs**

```csharp
// POST /api/v1/users/2fa/setup
group.MapPost("/2fa/setup", async (SetupTwoFactorCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return Results.Ok(result);
}).WithName("SetupTwoFactor");

// POST /api/v1/users/2fa/enable
group.MapPost("/2fa/enable", async (EnableTwoFactorCommand command, ISender sender) =>
{
    var backupCodes = await sender.Send(command);
    return Results.Ok(new { backupCodes });
}).WithName("EnableTwoFactor");

// POST /api/v1/users/2fa/disable
group.MapPost("/2fa/disable", async (DisableTwoFactorCommand command, ISender sender) =>
{
    await sender.Send(command);
    return Results.Ok(new { disabled = true });
}).WithName("DisableTwoFactor");

// POST /api/v1/users/2fa/verify
group.MapPost("/2fa/verify", async (VerifyTwoFactorCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return Results.Ok(result);
}).WithName("VerifyTwoFactor");
```

**Step 8: Commit**

```bash
git add src/Services/Identity/
git commit -m "feat(identity): add 2FA infrastructure, migration, and API endpoints"
```

---

### Task 4: Device Management — Domain Layer

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Domain/Entities/UserDevice.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/Ports/IUserDeviceRepository.cs`

**Step 1: Create UserDevice entity**

```csharp
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Entities;

public sealed class UserDevice : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string DeviceName { get; private set; } = null!;
    public string? DeviceType { get; private set; } // Desktop, Mobile, Tablet
    public string? Browser { get; private set; }
    public string? OperatingSystem { get; private set; }
    public string? IpAddress { get; private set; }
    public Guid? SessionId { get; private set; }
    public bool IsTrusted { get; private set; }
    public bool IsCurrentDevice { get; private set; }
    public DateTime LastActiveAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private UserDevice() { }

    public static UserDevice Create(
        Guid userId, string deviceName, string? deviceType,
        string? browser, string? operatingSystem, string? ipAddress,
        Guid? sessionId)
    {
        return new UserDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceName = deviceName,
            DeviceType = deviceType,
            Browser = browser,
            OperatingSystem = operatingSystem,
            IpAddress = ipAddress,
            SessionId = sessionId,
            IsTrusted = false,
            IsCurrentDevice = true,
            LastActiveAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateActivity(string? ipAddress, Guid? sessionId)
    {
        IpAddress = ipAddress;
        SessionId = sessionId;
        LastActiveAt = DateTime.UtcNow;
        IsCurrentDevice = true;
    }

    public void MarkTrusted() => IsTrusted = true;
    public void UnmarkTrusted() => IsTrusted = false;

    public void Revoke()
    {
        SessionId = null;
        IsCurrentDevice = false;
    }
}
```

**Step 2: Create IUserDeviceRepository**

```csharp
namespace FairBank.Identity.Domain.Ports;

public interface IUserDeviceRepository
{
    Task<IReadOnlyList<UserDevice>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserDevice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDevice?> FindByFingerprintAsync(Guid userId, string browser, string os, string deviceType, CancellationToken ct = default);
    Task AddAsync(UserDevice device, CancellationToken ct = default);
    Task UpdateAsync(UserDevice device, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

**Step 3: Commit**

```bash
git add src/Services/Identity/FairBank.Identity.Domain/
git commit -m "feat(identity): add UserDevice domain entity and repository port"
```

---

### Task 5: Device Management — Application, Infrastructure & API

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterDevice/RegisterDeviceCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterDevice/RegisterDeviceCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RevokeDevice/RevokeDeviceCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RevokeDevice/RevokeDeviceCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/TrustDevice/TrustDeviceCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/TrustDevice/TrustDeviceCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetDevices/GetDevicesQuery.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetDevices/GetDevicesQueryHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/DeviceResponse.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/UserDeviceRepository.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserDeviceConfiguration.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/IdentityDbContext.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/DependencyInjection.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`

**Step 1: Create DeviceResponse DTO**

```csharp
namespace FairBank.Identity.Application.Users.DTOs;

public sealed record DeviceResponse(
    Guid Id,
    string DeviceName,
    string? DeviceType,
    string? Browser,
    string? OperatingSystem,
    string? IpAddress,
    bool IsTrusted,
    bool IsCurrentDevice,
    DateTime LastActiveAt,
    DateTime CreatedAt);
```

**Step 2: Create RegisterDevice command + handler**

```csharp
// RegisterDeviceCommand.cs
public sealed record RegisterDeviceCommand(
    Guid UserId, string DeviceName, string? DeviceType,
    string? Browser, string? OperatingSystem, string? IpAddress,
    Guid? SessionId) : IRequest<DeviceResponse>;

// RegisterDeviceCommandHandler.cs
public sealed class RegisterDeviceCommandHandler(
    IUserDeviceRepository deviceRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<RegisterDeviceCommand, DeviceResponse>
{
    public async Task<DeviceResponse> Handle(RegisterDeviceCommand request, CancellationToken ct)
    {
        // Check if device already exists (same browser + OS + device type)
        var existing = await deviceRepo.FindByFingerprintAsync(
            request.UserId, request.Browser ?? "", request.OperatingSystem ?? "",
            request.DeviceType ?? "", ct);

        if (existing is not null)
        {
            existing.UpdateActivity(request.IpAddress, request.SessionId);
            await deviceRepo.UpdateAsync(existing, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return MapToResponse(existing);
        }

        var device = UserDevice.Create(
            request.UserId, request.DeviceName, request.DeviceType,
            request.Browser, request.OperatingSystem, request.IpAddress,
            request.SessionId);

        await deviceRepo.AddAsync(device, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapToResponse(device);
    }

    private static DeviceResponse MapToResponse(UserDevice d) => new(
        d.Id, d.DeviceName, d.DeviceType, d.Browser, d.OperatingSystem,
        d.IpAddress, d.IsTrusted, d.IsCurrentDevice, d.LastActiveAt, d.CreatedAt);
}
```

**Step 3: Create RevokeDevice, TrustDevice commands and GetDevices query**

Follow same pattern as above. RevokeDevice calls `device.Revoke()` and invalidates associated session on User. TrustDevice calls `device.MarkTrusted()`. GetDevices loads all devices for a user.

**Step 4: Create infrastructure (repository, configuration, migration)**

Follow the existing EF Core patterns. Register `IUserDeviceRepository → UserDeviceRepository` in DI. Add `DbSet<UserDevice>` to context. Create migration.

**Step 5: Add API endpoints**

```csharp
// GET /api/v1/users/{userId}/devices
group.MapGet("/{userId:guid}/devices", async (Guid userId, ISender sender) =>
{
    var result = await sender.Send(new GetDevicesQuery(userId));
    return Results.Ok(result);
}).WithName("GetDevices");

// POST /api/v1/users/devices
group.MapPost("/devices", async (RegisterDeviceCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return Results.Created($"/api/v1/users/devices/{result.Id}", result);
}).WithName("RegisterDevice");

// DELETE /api/v1/users/devices/{id}
group.MapDelete("/devices/{id:guid}", async (Guid id, RevokeDeviceCommand command, ISender sender) =>
{
    await sender.Send(command with { DeviceId = id });
    return Results.NoContent();
}).WithName("RevokeDevice");

// PUT /api/v1/users/devices/{id}/trust
group.MapPut("/devices/{id:guid}/trust", async (Guid id, ISender sender) =>
{
    await sender.Send(new TrustDeviceCommand(id));
    return Results.Ok(new { trusted = true });
}).WithName("TrustDevice");
```

**Step 6: Modify LoginUserCommandHandler to register device**

After successful login (non-2FA path), send `RegisterDeviceCommand` with User-Agent parsed data.

**Step 7: Commit**

```bash
git add src/Services/Identity/
git commit -m "feat(identity): add device management with registration, revoke, trust, and list"
```

---

### Task 6: Financial & Security Limits Enforcement

**Files:**
- Modify: `src/Services/Payments/FairBank.Payments.Application/Commands/SendPayment/SendPaymentCommandHandler.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Services/LimitEnforcementService.cs`
- Modify: `src/Services/Payments/FairBank.Payments.Infrastructure/Clients/AccountsServiceClient.cs`

**Step 1: Create LimitEnforcementService**

```csharp
namespace FairBank.Payments.Application.Services;

public sealed class LimitEnforcementService
{
    public static void EnforceSingleTransactionLimit(decimal amount, decimal? limit)
    {
        if (limit.HasValue && amount > limit.Value)
            throw new InvalidOperationException(
                $"Platba {amount} překračuje limit na jednu transakci ({limit.Value}).");
    }

    public static void EnforceDailyLimit(decimal todayTotal, decimal amount, decimal? dailyLimit)
    {
        if (dailyLimit.HasValue && (todayTotal + amount) > dailyLimit.Value)
            throw new InvalidOperationException(
                $"Platba překračuje denní limit. Dnes utraceno: {todayTotal}, limit: {dailyLimit.Value}.");
    }

    public static void EnforceMonthlyLimit(decimal monthTotal, decimal amount, decimal? monthlyLimit)
    {
        if (monthlyLimit.HasValue && (monthTotal + amount) > monthlyLimit.Value)
            throw new InvalidOperationException(
                $"Platba překračuje měsíční limit. Tento měsíc utraceno: {monthTotal}, limit: {monthlyLimit.Value}.");
    }

    public static void EnforceDailyCount(int todayCount, int? maxCount)
    {
        if (maxCount.HasValue && todayCount >= maxCount.Value)
            throw new InvalidOperationException(
                $"Překročen maximální počet plateb za den ({maxCount.Value}).");
    }

    public static void EnforceNightRestriction(bool nightEnabled)
    {
        if (!nightEnabled)
        {
            var hour = DateTime.UtcNow.Hour;
            if (hour >= 23 || hour < 6)
                throw new InvalidOperationException(
                    "Noční platby (23:00–06:00) jsou zakázány v bezpečnostním nastavení.");
        }
    }
}
```

**Step 2: Add limit data to AccountsServiceClient response**

Ensure the client fetches `AccountLimits` and `SecuritySettings` from the Accounts and Identity services when processing payments.

**Step 3: Modify SendPaymentCommandHandler**

Before executing the withdrawal, call:
```csharp
// 1. Check single transaction limit
LimitEnforcementService.EnforceSingleTransactionLimit(request.Amount, account.SingleTransactionLimit);

// 2. Query daily/monthly totals from payment history
var today = DateTime.UtcNow.Date;
var monthStart = new DateTime(today.Year, today.Month, 1);
var payments = await paymentRepo.GetByAccountIdAsync(senderAccount.Id);
var todayTotal = payments.Where(p => p.CreatedAt >= today && p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
var monthTotal = payments.Where(p => p.CreatedAt >= monthStart && p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
var todayCount = payments.Count(p => p.CreatedAt >= today && p.Status == PaymentStatus.Completed);

// 3. Enforce limits
LimitEnforcementService.EnforceDailyLimit(todayTotal, request.Amount, account.DailyTransactionLimit);
LimitEnforcementService.EnforceMonthlyLimit(monthTotal, request.Amount, account.MonthlyTransactionLimit);
LimitEnforcementService.EnforceDailyCount(todayCount, account.DailyTransactionCount);
```

**Step 4: Commit**

```bash
git add src/Services/Payments/
git commit -m "feat(payments): enforce financial and security limits during payment processing"
```

---

### Task 7: Frontend — Shared DTOs

**Files:**
- Create: `src/FairBank.Web.Shared/Models/TwoFactorSetupResponse.cs`
- Create: `src/FairBank.Web.Shared/Models/DeviceDto.cs`
- Create: `src/FairBank.Web.Shared/Models/CardDto.cs`
- Create: `src/FairBank.Web.Shared/Models/NotificationDto.cs`
- Create: `src/FairBank.Web.Shared/Models/NotificationPreferenceDto.cs`
- Modify: `src/FairBank.Web.Shared/Models/LoginResponse.cs`

**Step 1: Create all DTOs**

```csharp
// TwoFactorSetupResponse.cs
public class TwoFactorSetupResponse
{
    public string Secret { get; set; } = "";
    public string OtpAuthUri { get; set; } = "";
    public bool IsAlreadyEnabled { get; set; }
}

// DeviceDto.cs
public class DeviceDto
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = "";
    public string? DeviceType { get; set; }
    public string? Browser { get; set; }
    public string? OperatingSystem { get; set; }
    public string? IpAddress { get; set; }
    public bool IsTrusted { get; set; }
    public bool IsCurrentDevice { get; set; }
    public DateTime LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// CardDto.cs
public class CardDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string MaskedCardNumber { get; set; } = "";
    public string LastFourDigits { get; set; } = "";
    public string CardholderName { get; set; } = "";
    public DateTime ExpirationDate { get; set; }
    public string CardType { get; set; } = "";
    public string CardBrand { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal? DailyLimit { get; set; }
    public decimal? MonthlyLimit { get; set; }
    public bool OnlinePaymentsEnabled { get; set; }
    public bool ContactlessEnabled { get; set; }
    public bool HasPin { get; set; }
    public DateTime CreatedAt { get; set; }
}

// NotificationDto.cs
public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Type { get; set; } = "";
    public string Priority { get; set; } = "";
    public bool IsRead { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

// NotificationPreferenceDto.cs
public class NotificationPreferenceDto
{
    public bool TransactionNotifications { get; set; } = true;
    public bool SecurityNotifications { get; set; } = true;
    public bool CardNotifications { get; set; } = true;
    public bool LimitNotifications { get; set; } = true;
    public bool ChatNotifications { get; set; } = true;
    public bool EmailNotificationsEnabled { get; set; } = true;
}
```

**Step 2: Add RequiresTwoFactor to LoginResponse**

```csharp
public bool RequiresTwoFactor { get; set; }
```

**Step 3: Commit**

```bash
git add src/FairBank.Web.Shared/
git commit -m "feat(web): add shared DTOs for cards, notifications, devices, 2FA"
```

---

### Task 8: Frontend — Profile Page (2FA + Devices + Security Settings)

**Files:**
- Modify: `src/FairBank.Web.Profile/Pages/Profile.razor`
- Modify: `src/FairBank.Web.Shared/Services/IFairBankApi.cs`
- Modify: `src/FairBank.Web.Shared/Services/FairBankApiClient.cs`

**Step 1: Add API client methods**

Add to `IFairBankApi.cs` and implement in `FairBankApiClient.cs`:

```csharp
// 2FA
Task<TwoFactorSetupResponse?> SetupTwoFactorAsync(Guid userId);
Task<string[]?> EnableTwoFactorAsync(Guid userId, string code);
Task DisableTwoFactorAsync(Guid userId, string code);
Task<LoginResponse?> VerifyTwoFactorAsync(Guid userId, string code);

// Devices
Task<List<DeviceDto>> GetDevicesAsync(Guid userId);
Task RevokeDeviceAsync(Guid deviceId);
Task TrustDeviceAsync(Guid deviceId);

// Cards
Task<List<CardDto>> GetCardsAsync(Guid accountId);
Task<CardDto?> IssueCardAsync(Guid accountId, Guid userId, string holderName, string type, string brand);
Task BlockCardAsync(Guid cardId);
Task UnblockCardAsync(Guid cardId);
Task CancelCardAsync(Guid cardId);
Task SetCardLimitsAsync(Guid cardId, decimal? dailyLimit, decimal? monthlyLimit);
Task SetCardSettingsAsync(Guid cardId, bool onlineEnabled, bool contactlessEnabled);
Task SetCardPinAsync(Guid cardId, string pin);

// Notifications
Task<List<NotificationDto>> GetNotificationsAsync(Guid userId);
Task<int> GetUnreadNotificationCountAsync(Guid userId);
Task MarkNotificationReadAsync(Guid notificationId);
Task MarkAllNotificationsReadAsync(Guid userId);
Task<NotificationPreferenceDto?> GetNotificationPreferencesAsync(Guid userId);
Task UpdateNotificationPreferencesAsync(Guid userId, NotificationPreferenceDto prefs);
```

**Step 2: Expand Profile.razor with real sections**

Replace hardcoded security sections with:
- **2FA Section:** Setup wizard (show QR code via otpauth URI), code verification input, enable/disable, backup codes display
- **Devices Section:** List of devices with last active time, trust/revoke buttons, current device indicator
- **Security Settings:** AllowInternationalPayments toggle, NightTransactionsEnabled toggle, RequireApprovalAbove input
- **Notification Preferences:** Toggles per notification type and channel

**Step 3: Commit**

```bash
git add src/FairBank.Web.Profile/ src/FairBank.Web.Shared/
git commit -m "feat(web): expand profile page with 2FA setup, device management, security settings"
```

---

### Task 9: Frontend — Cards UI

**Files:**
- Modify: `src/FairBank.Web.Payments/Pages/Payments.razor` (add Cards tab)
  OR Create: `src/FairBank.Web.Payments/Pages/Cards.razor` (separate page)

**Step 1: Create Cards section in Payments page**

Add a "Karty" (Cards) tab to the existing Payments page or create a dedicated route. Include:
- **Card list:** Visual card representations showing masked number, holder name, expiry, type/brand, status
- **Issue card form:** Account selector, holder name, card type (Debit/Credit), brand (Visa/Mastercard)
- **Card detail panel:** Limits (daily/monthly sliders), settings (online/contactless toggles), block/unblock/cancel buttons, PIN management
- **Status indicators:** Active (green), Blocked (orange), Expired (grey), Cancelled (red)

**Step 2: Commit**

```bash
git add src/FairBank.Web.Payments/
git commit -m "feat(web): add payment cards management UI"
```

---

### Task 10: Frontend — Notifications UI

**Files:**
- Modify: `src/FairBank.Web/` (add notification bell to main layout)
- Create notification dropdown component

**Step 1: Add notification bell to header**

In the main layout, add:
- Bell icon with unread count badge
- Dropdown panel showing latest 10 notifications
- Click-through to related entity
- "Mark all as read" button
- Link to full notifications page

**Step 2: Connect SignalR NotificationHub**

On app startup, connect to `/notification-hub` and listen for real-time notifications. Update badge count on new notification.

**Step 3: Commit**

```bash
git add src/FairBank.Web/
git commit -m "feat(web): add notification bell, dropdown, and real-time updates"
```

---

### Task 11: Login Flow — 2FA Integration

**Files:**
- Modify: `src/FairBank.Web.Auth/Pages/Login.razor`
- Modify: `src/FairBank.Web.Shared/Services/AuthService.cs`

**Step 1: Update login flow**

When `LoginResponse.RequiresTwoFactor == true`:
1. Show 2FA code input instead of redirecting to dashboard
2. User enters TOTP code from authenticator app
3. Call `VerifyTwoFactorAsync(userId, code)`
4. On success, store session and redirect to dashboard
5. Allow backup code entry as fallback

**Step 2: Commit**

```bash
git add src/FairBank.Web.Auth/ src/FairBank.Web.Shared/
git commit -m "feat(web): integrate 2FA verification into login flow"
```

---

### Task 12: Unit Tests

**Files:**
- Create: `tests/FairBank.Identity.UnitTests/Application/SetupTwoFactorCommandHandlerTests.cs`
- Create: `tests/FairBank.Identity.UnitTests/Application/EnableTwoFactorCommandHandlerTests.cs`
- Create: `tests/FairBank.Identity.UnitTests/Application/RegisterDeviceCommandHandlerTests.cs`
- Create: `tests/FairBank.Identity.UnitTests/Helpers/TotpHelperTests.cs`

**Step 1: Test TotpHelper**

```csharp
public class TotpHelperTests
{
    [Fact]
    public void GenerateSecret_ShouldReturnBase32String()
    {
        var secret = TotpHelper.GenerateSecret();
        secret.Should().NotBeNullOrWhiteSpace();
        secret.Should().MatchRegex("^[A-Z2-7]+$");
    }

    [Fact]
    public void VerifyCode_WithValidCode_ShouldReturnTrue()
    {
        var secret = TotpHelper.GenerateSecret();
        // Generate current code manually for testing
        var code = GenerateCurrentCode(secret);
        TotpHelper.VerifyCode(secret, code).Should().BeTrue();
    }

    [Fact]
    public void GetOtpAuthUri_ShouldContainSecretAndIssuer()
    {
        var uri = TotpHelper.GetOtpAuthUri("JBSWY3DPEHPK3PXP", "test@fairbank.cz");
        uri.Should().Contain("otpauth://totp/");
        uri.Should().Contain("secret=JBSWY3DPEHPK3PXP");
        uri.Should().Contain("issuer=FairBank");
    }
}
```

**Step 2: Test command handlers**

Follow existing test patterns using NSubstitute + FluentAssertions + xUnit.

**Step 3: Commit**

```bash
git add tests/
git commit -m "test(identity): add unit tests for 2FA and device management"
```

---

## Summary

| Task | Scope | Estimated Steps |
|------|-------|-----------------|
| 1-3 | TOTP 2FA (Domain → Infra → API) | 24 steps |
| 4-5 | Device Management (Domain → API) | 14 steps |
| 6 | Financial Limits Enforcement | 4 steps |
| 7 | Frontend Shared DTOs | 3 steps |
| 8 | Profile Page (2FA + Devices + Settings) | 3 steps |
| 9 | Cards UI | 2 steps |
| 10 | Notifications UI | 3 steps |
| 11 | Login Flow 2FA Integration | 2 steps |
| 12 | Unit Tests | 3 steps |
