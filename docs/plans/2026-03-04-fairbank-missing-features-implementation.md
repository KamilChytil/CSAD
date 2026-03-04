# FairBank Missing Features Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement all 10 missing/incomplete features for the FairBank banking application to production quality.

**Architecture:** .NET 10 microservices (Identity, Accounts, Payments, Chat, Products) + 2 new services (Cards, Notifications). CQRS via MediatR, Event Sourcing (Marten) for Accounts, EF Core for other services. Blazor WASM frontend with module-based architecture. YARP API Gateway. PostgreSQL with schema isolation.

**Tech Stack:** .NET 10, Blazor WASM, MediatR 14, FluentValidation 12, EF Core 10, Marten 8, BCrypt, SignalR, YARP, PostgreSQL 16, xUnit + NSubstitute + FluentAssertions

---

## Task 1: Registration – Persist KYC Data (40% → 100%)

**Files:**
- Modify: `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/ValueObjects/Address.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/ValueObjects/PhoneNumber.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommand.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandHandler.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandValidator.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/UserResponse.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/IdentityDbContext.cs`
- Create: EF Core migration
- Test: `tests/FairBank.Identity.UnitTests/Domain/PhoneNumberTests.cs`
- Test: `tests/FairBank.Identity.UnitTests/Domain/AddressTests.cs`
- Test: `tests/FairBank.Identity.UnitTests/Application/RegisterUserCommandHandlerTests.cs`

**Step 1: Create PhoneNumber Value Object**

Create `src/Services/Identity/FairBank.Identity.Domain/ValueObjects/PhoneNumber.cs`:
```csharp
using FairBank.SharedKernel.Domain;
using System.Text.RegularExpressions;

namespace FairBank.Identity.Domain.ValueObjects;

public sealed partial class PhoneNumber : ValueObject
{
    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber Create(string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phone, nameof(phone));
        var normalized = NormalizeRegex().Replace(phone.Trim(), "");

        if (normalized.Length < 9 || normalized.Length > 15)
            throw new ArgumentException($"Invalid phone number: {phone}", nameof(phone));

        return new PhoneNumber(normalized);
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    [GeneratedRegex(@"[\s\-\(\)]")]
    private static partial Regex NormalizeRegex();
}
```

**Step 2: Create Address Value Object**

Create `src/Services/Identity/FairBank.Identity.Domain/ValueObjects/Address.cs`:
```csharp
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string ZipCode { get; }
    public string Country { get; }

    private Address(string street, string city, string zipCode, string country)
    {
        Street = street;
        City = city;
        ZipCode = zipCode;
        Country = country;
    }

    public static Address Create(string street, string city, string zipCode, string country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street, nameof(street));
        ArgumentException.ThrowIfNullOrWhiteSpace(city, nameof(city));
        ArgumentException.ThrowIfNullOrWhiteSpace(zipCode, nameof(zipCode));
        ArgumentException.ThrowIfNullOrWhiteSpace(country, nameof(country));

        var normalizedZip = zipCode.Trim().Replace(" ", "");
        if (normalizedZip.Length < 5)
            throw new ArgumentException("ZIP code must be at least 5 characters.", nameof(zipCode));

        return new Address(street.Trim(), city.Trim(), normalizedZip, country.Trim());
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Street;
        yield return City;
        yield return ZipCode;
        yield return Country;
    }
}
```

**Step 3: Extend User entity**

Add to `User.cs`:
```csharp
public string? PersonalIdNumber { get; private set; }
public DateOnly? DateOfBirth { get; private set; }
public PhoneNumber? PhoneNumber { get; private set; }
public Address? Address { get; private set; }
public DateTime? AgreedToTermsAt { get; private set; }
public bool IsEmailVerified { get; private set; }
public string? EmailVerificationToken { get; private set; }
public DateTime? EmailVerificationTokenExpiresAt { get; private set; }
```

Extend `Create` factory method to accept optional KYC parameters. Add `VerifyEmail()` and `GenerateEmailVerificationToken()` methods.

**Step 4: Extend RegisterUserCommand**

```csharp
public sealed record RegisterUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    UserRole Role = UserRole.Client,
    string? PersonalIdNumber = null,
    DateOnly? DateOfBirth = null,
    string? Phone = null,
    string? Street = null,
    string? City = null,
    string? ZipCode = null,
    string? Country = null) : IRequest<UserResponse>;
```

**Step 5: Extend validator with KYC rules**

Add validation for PersonalIdNumber (Czech rodné číslo format), DateOfBirth (min 15 years), Phone, Address fields.

**Step 6: Extend handler to persist KYC data**

Update `RegisterUserCommandHandler` to pass KYC fields to `User.Create()` and generate email verification token.

**Step 7: Update UserResponse DTO**

Add KYC fields to response.

**Step 8: Configure EF Core mapping for Value Objects**

In `IdentityDbContext.OnModelCreating`, configure `OwnsOne` for Address, conversion for PhoneNumber.

**Step 9: Create EF Core migration**

Run: `dotnet ef migrations add AddKycFields -p src/Services/Identity/FairBank.Identity.Infrastructure -s src/Services/Identity/FairBank.Identity.Api`

**Step 10: Write unit tests**

Test Value Objects, extended validator, and handler.

**Step 11: Commit**

```bash
git add -A && git commit -m "feat(identity): persist KYC data from registration form"
```

---

## Task 2: Email Verification & Password Management (Identity 10% → 60%)

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/VerifyEmail/VerifyEmailCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/VerifyEmail/VerifyEmailCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ForgotPassword/ForgotPasswordCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ForgotPassword/ForgotPasswordCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ResetPassword/ResetPasswordCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ResetPassword/ResetPasswordCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ChangePassword/ChangePasswordCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ChangePassword/ChangePasswordCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Ports/IEmailSender.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Email/SmtpEmailSender.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`
- Create: EF Core migration
- Test: `tests/FairBank.Identity.UnitTests/Application/VerifyEmailCommandHandlerTests.cs`
- Test: `tests/FairBank.Identity.UnitTests/Application/ForgotPasswordCommandHandlerTests.cs`
- Test: `tests/FairBank.Identity.UnitTests/Application/ChangePasswordCommandHandlerTests.cs`

**Step 1: Create IEmailSender port**

```csharp
namespace FairBank.Identity.Application.Ports;

public interface IEmailSender
{
    Task SendEmailVerificationAsync(string to, string token, CancellationToken ct = default);
    Task SendPasswordResetAsync(string to, string token, CancellationToken ct = default);
    Task SendSecurityAlertAsync(string to, string subject, string message, CancellationToken ct = default);
}
```

**Step 2: Add domain methods to User entity**

```csharp
public void GenerateEmailVerificationToken()
{
    EmailVerificationToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
}

public void VerifyEmail(string token)
{
    if (IsEmailVerified)
        throw new InvalidOperationException("Email is already verified.");
    if (EmailVerificationToken != token)
        throw new InvalidOperationException("Invalid verification token.");
    if (EmailVerificationTokenExpiresAt < DateTime.UtcNow)
        throw new InvalidOperationException("Verification token has expired.");

    IsEmailVerified = true;
    EmailVerificationToken = null;
    EmailVerificationTokenExpiresAt = null;
    UpdatedAt = DateTime.UtcNow;
}

public string GeneratePasswordResetToken()
{
    PasswordResetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
    UpdatedAt = DateTime.UtcNow;
    return PasswordResetToken;
}

public void ResetPassword(string token, string newPasswordHash)
{
    if (PasswordResetToken != token)
        throw new InvalidOperationException("Invalid reset token.");
    if (PasswordResetTokenExpiresAt < DateTime.UtcNow)
        throw new InvalidOperationException("Reset token has expired.");

    PasswordHash = newPasswordHash;
    PasswordResetToken = null;
    PasswordResetTokenExpiresAt = null;
    ActiveSessionId = null;
    UpdatedAt = DateTime.UtcNow;
}

public void ChangePassword(string currentPasswordHash, string newPasswordHash)
{
    if (!BCrypt.Net.BCrypt.Verify(currentPasswordHash, PasswordHash))
        throw new InvalidOperationException("Current password is incorrect.");

    PasswordHash = newPasswordHash;
    UpdatedAt = DateTime.UtcNow;
}
```

**Step 3: Create VerifyEmailCommand + Handler**

```csharp
public sealed record VerifyEmailCommand(string Token) : IRequest<bool>;
```

Handler: Find user by token, call `VerifyEmail()`, save.

**Step 4: Create ForgotPasswordCommand + Handler**

```csharp
public sealed record ForgotPasswordCommand(string Email) : IRequest;
```

Handler: Find user by email, generate reset token, send email. Always return success (no user enumeration).

**Step 5: Create ResetPasswordCommand + Handler**

```csharp
public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<bool>;
```

Handler: Find user by reset token, validate token, hash new password, call `ResetPassword()`.

**Step 6: Create ChangePasswordCommand + Handler**

```csharp
public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<bool>;
```

**Step 7: Create SmtpEmailSender infrastructure**

```csharp
public sealed class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendEmailVerificationAsync(string to, string token, CancellationToken ct = default)
    {
        var verifyUrl = $"{config["App:BaseUrl"]}/verify-email?token={Uri.EscapeDataString(token)}";
        await SendAsync(to, "Ověření emailové adresy – FairBank",
            $"Klikněte na odkaz pro ověření: {verifyUrl}", ct);
    }
    // ... other methods
}
```

**Step 8: Register endpoints**

```
POST /api/v1/users/verify-email
POST /api/v1/users/resend-verification
POST /api/v1/users/forgot-password
POST /api/v1/users/reset-password
POST /api/v1/users/change-password
```

**Step 9: Update LoginUserCommandHandler to check IsEmailVerified**

**Step 10: Add User fields + migration**

```csharp
public string? PasswordResetToken { get; private set; }
public DateTime? PasswordResetTokenExpiresAt { get; private set; }
```

**Step 11: Write unit tests and commit**

```bash
git commit -m "feat(identity): add email verification, password reset, password change"
```

---

## Task 3: Two-Factor Authentication (Identity → 80%)

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Domain/Entities/TwoFactorAuth.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/Setup2fa/Setup2faCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/Setup2fa/Setup2faCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/Verify2fa/Verify2faCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/Verify2fa/Verify2faCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/Disable2fa/Disable2faCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/Disable2fa/Disable2faCommandHandler.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/LoginUser/LoginUserCommandHandler.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`
- Add package: `Otp.NET` to `Directory.Packages.props`
- Test: `tests/FairBank.Identity.UnitTests/Application/Setup2faCommandHandlerTests.cs`

**Step 1: Add Otp.NET package**

Add to `Directory.Packages.props`:
```xml
<PackageVersion Include="Otp.NET" Version="1.4.0" />
```

**Step 2: Create TwoFactorAuth entity**

Stored on User or as separate entity with SecretKey (encrypted), IsEnabled, BackupCodes (JSON array of hashed codes).

**Step 3: Setup2fa endpoint**

Returns secret key + QR code URI (otpauth://totp/FairBank:{email}?secret={base32}&issuer=FairBank).

**Step 4: Verify2fa – activates 2FA after user confirms they can generate codes**

**Step 5: Modify login flow**

If 2FA enabled, login returns `Requires2fa = true` with a temporary token. Client sends code + temp token to `/api/v1/users/2fa/login-verify`.

**Step 6: Disable2fa with password confirmation**

**Step 7: Backup codes – generate 10, store hashed, each usable once**

**Step 8: Write tests and commit**

```bash
git commit -m "feat(identity): add TOTP two-factor authentication with backup codes"
```

---

## Task 4: Device Management (15% → 100%)

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Domain/Entities/UserDevice.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/Enums/DeviceType.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterDevice/RegisterDeviceCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterDevice/RegisterDeviceCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RemoveDevice/RemoveDeviceCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RemoveDevice/RemoveDeviceCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/TrustDevice/TrustDeviceCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetDevices/GetDevicesQuery.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetDevices/GetDevicesQueryHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/DeviceResponse.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Ports/IDeviceRepository.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/DeviceRepository.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/LoginUser/LoginUserCommandHandler.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/IdentityDbContext.cs`
- Create: EF Core migration
- Test: `tests/FairBank.Identity.UnitTests/Application/GetDevicesQueryHandlerTests.cs`

**Step 1: Create DeviceType enum and UserDevice entity**

```csharp
public sealed class UserDevice : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string DeviceName { get; private set; } = null!;
    public DeviceType DeviceType { get; private set; }
    public string? Browser { get; private set; }
    public string? OperatingSystem { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTime LastActiveAt { get; private set; }
    public Guid SessionId { get; private set; }
    public bool IsTrusted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    // ... factory + methods
}
```

**Step 2: Create IDeviceRepository port and EF implementation**

**Step 3: Modify LoginUserCommandHandler**

Parse User-Agent header → extract browser, OS, device type. Create/update UserDevice on login. Support multiple sessions (change from single ActiveSessionId to list of devices with sessions).

**Step 4: Create query and mutation endpoints**

```
GET    /api/v1/users/devices
DELETE /api/v1/users/devices/{id}
PUT    /api/v1/users/devices/{id}/trust
```

**Step 5: EF Core migration, tests, commit**

```bash
git commit -m "feat(identity): add device management with multi-session support"
```

---

## Task 5: Account Management Improvements (85% → 100%)

**Files:**
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/AccountClosed.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/AccountRenamed.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/CloseAccount/CloseAccountCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/CloseAccount/CloseAccountCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/RenameAccount/RenameAccountCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/RenameAccount/RenameAccountCommandHandler.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/Account.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Api/Endpoints/AccountEndpoints.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Application/DTOs/AccountResponse.cs`
- Test: `tests/FairBank.Accounts.UnitTests/Domain/AccountTests.cs` (extend)
- Test: `tests/FairBank.Accounts.UnitTests/Application/CloseAccountCommandHandlerTests.cs`

**Step 1: Add `Alias` property and `Close()`/`Rename()` methods to Account aggregate**

```csharp
[JsonInclude] public string? Alias { get; private set; }

public void Close()
{
    EnsureActive();
    if (Balance.Amount != 0)
        throw new InvalidOperationException("Cannot close account with non-zero balance.");
    IsActive = false;
    RaiseEvent(new AccountClosed(Id, DateTime.UtcNow));
}

public void Rename(string alias)
{
    EnsureActive();
    Alias = alias?.Trim();
    RaiseEvent(new AccountRenamed(Id, Alias, DateTime.UtcNow));
}

public void Apply(AccountClosed _) { IsActive = false; }
public void Apply(AccountRenamed @event) { Alias = @event.Alias; }
```

**Step 2: Create event records, commands, handlers**

**Step 3: Add endpoints**

```
POST /api/v1/accounts/{id}/close
PUT  /api/v1/accounts/{id}/alias
```

**Step 4: Update AccountResponse to include Alias**

**Step 5: Tests and commit**

```bash
git commit -m "feat(accounts): add account close and rename functionality"
```

---

## Task 6: Payment Cards – New Microservice (0% → 100%)

**Files to create – full microservice following existing patterns:**

```
src/Services/Cards/
├── FairBank.Cards.Domain/
│   ├── FairBank.Cards.Domain.csproj
│   ├── Aggregates/Card.cs
│   ├── ValueObjects/CardNumber.cs
│   ├── Enums/CardType.cs
│   ├── Enums/CardBrand.cs
│   ├── Enums/CardStatus.cs
│   └── Events/CardIssued.cs, CardBlocked.cs, CardUnblocked.cs,
│         CardCancelled.cs, CardLimitsSet.cs, CardRenewed.cs
├── FairBank.Cards.Application/
│   ├── FairBank.Cards.Application.csproj
│   ├── DependencyInjection.cs
│   ├── Ports/ICardRepository.cs
│   ├── DTOs/CardResponse.cs
│   ├── Commands/
│   │   ├── IssueCard/IssueCardCommand.cs + Handler + Validator
│   │   ├── BlockCard/BlockCardCommand.cs + Handler
│   │   ├── UnblockCard/UnblockCardCommand.cs + Handler
│   │   ├── CancelCard/CancelCardCommand.cs + Handler
│   │   ├── SetCardLimits/SetCardLimitsCommand.cs + Handler + Validator
│   │   ├── SetCardSettings/SetCardSettingsCommand.cs + Handler
│   │   ├── RenewCard/RenewCardCommand.cs + Handler
│   │   ├── SetPin/SetPinCommand.cs + Handler
│   │   └── ChangePin/ChangePinCommand.cs + Handler
│   └── Queries/
│       ├── GetCardsByAccount/GetCardsByAccountQuery.cs + Handler
│       ├── GetCardsByUser/GetCardsByUserQuery.cs + Handler
│       └── GetCardById/GetCardByIdQuery.cs + Handler
├── FairBank.Cards.Infrastructure/
│   ├── FairBank.Cards.Infrastructure.csproj
│   ├── DependencyInjection.cs
│   └── Persistence/
│       ├── CardsDbContext.cs
│       └── CardRepository.cs
└── FairBank.Cards.Api/
    ├── FairBank.Cards.Api.csproj
    ├── Program.cs
    ├── Dockerfile
    ├── appsettings.json
    └── Endpoints/CardEndpoints.cs
```

**Step 1: Create solution structure**

Create all 4 projects (Domain, Application, Infrastructure, Api) with proper project references following the exact pattern of Identity/Payments services.

**Step 2: Domain layer**

Card aggregate with:
- `Id`, `AccountId`, `UserId`, `CardNumber` (ValueObject – masked), `CardholderName`, `ExpirationDate`, `CardType`, `CardBrand`, `Status`, `DailyLimit` (Money), `MonthlyLimit` (Money), `OnlinePaymentsEnabled`, `ContactlessEnabled`, `PinHash`, `CreatedAt`, `UpdatedAt`
- Factory: `Issue(accountId, userId, holderName, type, brand)`
- Methods: `Block()`, `Unblock()`, `Cancel()`, `SetLimits()`, `SetSettings()`, `Renew()`, `SetPin()`, `ChangePin()`

CardNumber ValueObject generates realistic-looking 16-digit number, stores last 4, masks rest.

**Step 3: Application layer**

CQRS commands and queries with MediatR, FluentValidation for IssueCard and SetCardLimits.

**Step 4: Infrastructure layer**

EF Core `CardsDbContext` with schema `cards_service`. CardRepository.

**Step 5: API layer**

Minimal API endpoints matching the design. Program.cs following Chat/Payments pattern.

**Step 6: Docker + Gateway integration**

Add to `docker-compose.yml`:
```yaml
cards-api:
  build:
    context: .
    dockerfile: src/Services/Cards/FairBank.Cards.Api/Dockerfile
  container_name: fairbank-cards-api
  expose:
    - "8080"
  environment:
    ASPNETCORE_ENVIRONMENT: Development
    ConnectionStrings__DefaultConnection: "Host=postgres-primary;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=cards_service"
  depends_on:
    postgres-primary:
      condition: service_healthy
  networks:
    - backend
```

Add YARP routes in `appsettings.json`:
```json
"cards-route": {
  "ClusterId": "cards-cluster",
  "Match": { "Path": "/api/v1/cards/{**catch-all}" }
},
"cards-cluster": {
  "Destinations": { "cards-api": { "Address": "http://cards-api:8080" } }
}
```

**Step 7: Frontend – FairBank.Web.Cards module**

Create `src/FairBank.Web.Cards/` with:
- `Pages/Cards.razor` – card list, visual card display, management UI
- Add to `IFairBankApi` and `FairBankApiClient`
- Add to `App.razor` router
- Add navigation item

**Step 8: Unit tests**

```
tests/FairBank.Cards.UnitTests/
├── Domain/CardTests.cs
├── Domain/CardNumberTests.cs
├── Application/IssueCardCommandHandlerTests.cs
├── Application/BlockCardCommandHandlerTests.cs
└── Application/SetCardLimitsCommandHandlerTests.cs
```

**Step 9: Commit**

```bash
git commit -m "feat(cards): add payment cards microservice with full CRUD"
```

---

## Task 7: Financial & Security Limits (50% → 100%)

**Files:**
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/ValueObjects/AccountLimits.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/AccountLimitsSet.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/SetAccountLimits/SetAccountLimitsCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/SetAccountLimits/SetAccountLimitsCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetAccountLimits/GetAccountLimitsQuery.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetAccountLimits/GetAccountLimitsQueryHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/DTOs/AccountLimitsResponse.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/ValueObjects/SecuritySettings.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/SetSecuritySettings/SetSecuritySettingsCommand.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/Account.cs`
- Modify: `src/Services/Payments/FairBank.Payments.Application/Commands/SendPayment/SendPaymentCommandHandler.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Api/Endpoints/AccountEndpoints.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`
- Test: `tests/FairBank.Accounts.UnitTests/Domain/AccountLimitsTests.cs`

**Step 1: Create AccountLimits ValueObject on Account aggregate**

```csharp
public sealed class AccountLimits : ValueObject
{
    public decimal DailyTransactionLimit { get; }
    public decimal MonthlyTransactionLimit { get; }
    public decimal SingleTransactionLimit { get; }
    public int DailyTransactionCount { get; }
    public decimal OnlinePaymentLimit { get; }
    // ... Create, GetAtomicValues
}
```

**Step 2: Add limits to Account aggregate + Apply methods**

**Step 3: Create SetAccountLimitsCommand**

**Step 4: Create GetAccountLimitsQuery** that also calculates current usage (requires querying payment history)

**Step 5: Add SecuritySettings to User entity**

```csharp
public bool AllowInternationalPayments { get; private set; } = true;
public bool NightTransactionsEnabled { get; private set; } = true;
public decimal? RequireApprovalAbove { get; private set; }
```

**Step 6: Modify SendPaymentCommandHandler to enforce limits**

Check daily/monthly cumulative amounts against limits before executing payment.

**Step 7: Add endpoints, tests, commit**

```bash
git commit -m "feat(accounts): add granular financial and security limits with enforcement"
```

---

## Task 8: Transaction Statistics & History (35% → 100%)

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Domain/Enums/PaymentCategory.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Queries/GetPaymentStatistics/GetPaymentStatisticsQuery.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Queries/GetPaymentStatistics/GetPaymentStatisticsQueryHandler.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Queries/SearchPayments/SearchPaymentsQuery.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Queries/SearchPayments/SearchPaymentsQueryHandler.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Queries/ExportPayments/ExportPaymentsQuery.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Queries/ExportPayments/ExportPaymentsQueryHandler.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Commands/SetPaymentCategory/SetPaymentCategoryCommand.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/DTOs/PaymentStatisticsResponse.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/DTOs/PagedPaymentsResponse.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Services/PaymentCategorizer.cs`
- Modify: `src/Services/Payments/FairBank.Payments.Domain/Entities/Payment.cs` (add Category field)
- Modify: `src/Services/Payments/FairBank.Payments.Api/Endpoints/PaymentEndpoints.cs`
- Create: EF Core migration
- Modify: `src/FairBank.Web.Overview/Pages/Overview.razor` (add charts)
- Test: `tests/FairBank.Payments.UnitTests/Application/GetPaymentStatisticsQueryHandlerTests.cs`

**Step 1: Add PaymentCategory enum and Category field to Payment**

```csharp
public enum PaymentCategory
{
    Other = 0, Housing = 1, Food = 2, Transport = 3,
    Entertainment = 4, Health = 5, Shopping = 6,
    Savings = 7, Salary = 8, Utilities = 9
}
```

**Step 2: Create PaymentCategorizer service**

Rule-based auto-categorization from description keywords (e.g., "nájem" → Housing, "potraviny" → Food).

**Step 3: Create SearchPaymentsQuery with pagination + filtering**

Accepts: AccountId, DateFrom, DateTo, MinAmount, MaxAmount, Category, Status, SearchText, Page, PageSize, SortBy, SortDirection.

**Step 4: Create GetPaymentStatisticsQuery**

Returns: TotalIncome, TotalExpenses, TransactionCount, AverageTransaction, CategoryBreakdown, MonthlyTrend.

**Step 5: Create ExportPaymentsQuery**

Returns CSV or PDF bytes. CSV with System.Text, PDF with basic HTML-to-PDF or simple text format.

**Step 6: Add endpoints**

```
GET /api/v1/payments/account/{id}?page=&pageSize=&dateFrom=&...  (extend existing)
GET /api/v1/payments/account/{id}/statistics?period=&dateFrom=&dateTo=
GET /api/v1/payments/account/{id}/export?format=csv&dateFrom=&dateTo=
PUT /api/v1/payments/{id}/category
```

**Step 7: Update frontend IFairBankApi + Overview.razor with statistics display**

**Step 8: Migration, tests, commit**

```bash
git commit -m "feat(payments): add statistics, search, filtering, categorization, export"
```

---

## Task 9: Notifications – New Microservice (5% → 100%)

**Files to create – full microservice:**

```
src/Services/Notifications/
├── FairBank.Notifications.Domain/
│   ├── FairBank.Notifications.Domain.csproj
│   ├── Entities/Notification.cs
│   ├── Entities/NotificationPreference.cs
│   └── Enums/NotificationType.cs, NotificationPriority.cs,
│         NotificationChannel.cs, NotificationStatus.cs
├── FairBank.Notifications.Application/
│   ├── FairBank.Notifications.Application.csproj
│   ├── DependencyInjection.cs
│   ├── Ports/INotificationRepository.cs
│   ├── Ports/INotificationPreferenceRepository.cs
│   ├── DTOs/NotificationResponse.cs, NotificationPreferenceResponse.cs
│   ├── Commands/
│   │   ├── CreateNotification/CreateNotificationCommand.cs + Handler
│   │   ├── MarkAsRead/MarkAsReadCommand.cs + Handler
│   │   ├── MarkAllAsRead/MarkAllAsReadCommand.cs + Handler
│   │   ├── DeleteNotification/DeleteNotificationCommand.cs + Handler
│   │   └── UpdatePreferences/UpdatePreferencesCommand.cs + Handler
│   ├── Queries/
│   │   ├── GetNotifications/GetNotificationsQuery.cs + Handler
│   │   ├── GetUnreadCount/GetUnreadCountQuery.cs + Handler
│   │   └── GetPreferences/GetPreferencesQuery.cs + Handler
│   └── Hubs/NotificationHub.cs
├── FairBank.Notifications.Infrastructure/
│   ├── FairBank.Notifications.Infrastructure.csproj
│   ├── DependencyInjection.cs
│   └── Persistence/
│       ├── NotificationsDbContext.cs
│       ├── NotificationRepository.cs
│       └── NotificationPreferenceRepository.cs
└── FairBank.Notifications.Api/
    ├── FairBank.Notifications.Api.csproj
    ├── Program.cs
    ├── Dockerfile
    ├── appsettings.json
    └── Endpoints/NotificationEndpoints.cs
```

**Step 1: Create domain layer**

Notification entity with Type, Priority, Channel, Status, RelatedEntityType/Id. NotificationPreference per user.

**Step 2: Create application layer with NotificationHub (SignalR)**

```csharp
public sealed class NotificationHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
    }
}
```

**Step 3: Create infrastructure with EF Core (schema: notifications_service)**

**Step 4: Create API endpoints**

```
POST   /api/v1/notifications/              → Create (internal, from other services)
GET    /api/v1/notifications/?userId=&type=&page=&pageSize=
GET    /api/v1/notifications/unread-count?userId=
PUT    /api/v1/notifications/{id}/read
PUT    /api/v1/notifications/read-all?userId=
DELETE /api/v1/notifications/{id}
GET    /api/v1/notifications/preferences?userId=
PUT    /api/v1/notifications/preferences
```

**Step 5: Docker + Gateway integration**

Same pattern as Cards service: add `notifications-api` to docker-compose, YARP routes, and notification-hub SignalR route.

**Step 6: Create INotificationClient typed HTTP client**

In other services (Payments, Identity, Cards), add `INotificationClient` to call notification API when events occur:
- Payment completed → Transaction notification
- Login from new device → Security notification
- Card blocked → Card notification

**Step 7: Frontend integration**

- Add `NotificationService` to `FairBank.Web.Shared/Services/`
- Connect to `NotificationHub` on app start
- Add notification bell with badge in `MainLayout.razor`
- Create notification dropdown/panel component
- Update Profile.razor toggles to actually persist preferences

**Step 8: Tests and commit**

```bash
git commit -m "feat(notifications): add notifications microservice with SignalR real-time delivery"
```

---

## Task 10: QR Payments (Payments 90% → 100%)

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Application/Queries/GenerateQrCode/GenerateQrCodeQuery.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Queries/GenerateQrCode/GenerateQrCodeQueryHandler.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Commands/ParseQrPayment/ParseQrPaymentCommand.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Services/SpaydGenerator.cs`
- Create: `src/Services/Payments/FairBank.Payments.Application/Services/SpaydParser.cs`
- Modify: `src/Services/Payments/FairBank.Payments.Api/Endpoints/PaymentEndpoints.cs`
- Add package: `QRCoder` to `Directory.Packages.props`
- Modify: `src/FairBank.Web.Payments/Pages/Payments.razor` (QR button functionality)
- Test: `tests/FairBank.Payments.UnitTests/Services/SpaydGeneratorTests.cs`
- Test: `tests/FairBank.Payments.UnitTests/Services/SpaydParserTests.cs`

**Step 1: Add QRCoder package**

```xml
<PackageVersion Include="QRCoder" Version="1.6.0" />
```

**Step 2: Create SpaydGenerator**

SPAYD format: `SPD*1.0*ACC:{IBAN}*AM:{amount}*CC:CZK*MSG:{message}*`

**Step 3: Create SpaydParser**

Parse SPAYD string → extract account number, amount, message.

**Step 4: Create GenerateQrCodeQuery**

Returns Base64-encoded PNG QR code image.

**Step 5: Add endpoints**

```
GET  /api/v1/payments/qr-code?accountNumber=&amount=&message=
POST /api/v1/payments/parse-qr
```

**Step 6: Frontend – QR display modal + camera/upload for scanning**

**Step 7: Tests and commit**

```bash
git commit -m "feat(payments): add QR payment generation and parsing (SPAYD)"
```

---

## Task 11: Chat Improvements (80% → 100%)

**Files:**
- Create: `src/Services/Chat/FairBank.Chat.Domain/Entities/ChatAttachment.cs`
- Modify: `src/Services/Chat/FairBank.Chat.Domain/Aggregates/ChatMessage.cs` (add ReadAt)
- Create: `src/Services/Chat/FairBank.Chat.Application/Messages/Commands/UploadAttachment/UploadAttachmentCommand.cs`
- Create: `src/Services/Chat/FairBank.Chat.Application/Messages/Queries/SearchMessages/SearchMessagesQuery.cs`
- Create: `src/Services/Chat/FairBank.Chat.Application/Messages/Commands/MarkMessageRead/MarkMessageReadCommand.cs`
- Modify: `src/Services/Chat/FairBank.Chat.Application/Hubs/ChatHub.cs` (typing indicators, read receipts)
- Modify: `src/Services/Chat/FairBank.Chat.Api/Program.cs` (new endpoints)
- Modify: `src/FairBank.Web.Shared/Services/Chat/ChatService.cs`
- Create: EF Core migration

**Step 1: Add ReadAt to ChatMessage**

```csharp
public DateTime? ReadAt { get; private set; }

public void MarkAsRead()
{
    if (ReadAt is null)
        ReadAt = DateTime.UtcNow;
}
```

**Step 2: Create ChatAttachment entity**

```csharp
public sealed class ChatAttachment : Entity<Guid>
{
    public Guid MessageId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long FileSize { get; private set; }
    public string StoragePath { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
}
```

**Step 3: Add SignalR typing indicators**

In ChatHub:
```csharp
public async Task StartTyping(Guid conversationId, Guid userId, string userName)
{
    await Clients.OthersInGroup($"conv-{conversationId}")
        .SendAsync("UserTyping", new { userId, userName });
}

public async Task StopTyping(Guid conversationId, Guid userId)
{
    await Clients.OthersInGroup($"conv-{conversationId}")
        .SendAsync("UserStoppedTyping", new { userId });
}

public async Task MarkAsRead(Guid messageId, Guid userId)
{
    var command = new MarkMessageReadCommand(messageId);
    await sender.Send(command);

    // Notify sender
    await Clients.OthersInGroup($"conv-{conversationId}")
        .SendAsync("MessageRead", new { messageId, userId, readAt = DateTime.UtcNow });
}
```

**Step 4: Add search endpoint**

```
GET /api/v1/chat/conversations/{id}/messages/search?query=&page=&pageSize=
```

**Step 5: Add attachment upload/download**

```
POST /api/v1/chat/messages/{id}/attachments  (multipart)
GET  /api/v1/chat/attachments/{id}/download
```

**Step 6: Update frontend ChatService + UI**

**Step 7: Migration, tests, commit**

```bash
git commit -m "feat(chat): add attachments, typing indicators, read receipts, search"
```

---

## Task 12: Frontend Integration & Polish

**Files:**
- Modify: `src/FairBank.Web.Shared/Services/IFairBankApi.cs`
- Modify: `src/FairBank.Web.Shared/Services/FairBankApiClient.cs`
- Modify: `src/FairBank.Web.Shared/Models/` (add new DTOs)
- Modify: `src/FairBank.Web/App.razor` (add Cards route)
- Modify: `src/FairBank.Web/Layout/MainLayout.razor` (notification bell)
- Modify: `src/FairBank.Web/Layout/SideNav.razor` + `BottomNav.razor` (Cards nav item)
- Modify: `src/FairBank.Web.Profile/Pages/Profile.razor` (devices, security settings, working toggles)
- Modify: `src/FairBank.Web.Overview/Pages/Overview.razor` (multi-account, statistics)
- Modify: `src/FairBank.Web.Auth/Pages/Login.razor` (2FA flow)
- Create: `src/FairBank.Web.Cards/Pages/Cards.razor`
- Create: `src/FairBank.Web.Shared/Services/NotificationService.cs`
- Create: `src/FairBank.Web.Shared/Components/NotificationBell.razor`
- Create: `src/FairBank.Web.Shared/Components/NotificationPanel.razor`
- Create: `src/FairBank.Web.Shared/Components/DeviceList.razor`

**Step 1: Add all new API methods to IFairBankApi and FairBankApiClient**

Cards, Notifications, Devices, Statistics, Limits, 2FA, Password management endpoints.

**Step 2: Add new DTOs/Models**

CardResponse, NotificationResponse, DeviceResponse, PaymentStatisticsResponse, AccountLimitsResponse, etc.

**Step 3: Create Cards page**

Visual card component with card number, name, expiration. Management controls.

**Step 4: Create NotificationService + Bell component**

SignalR connection to NotificationHub. Badge with unread count. Dropdown panel.

**Step 5: Update Profile page**

- Device list with remote logout
- Working notification preference toggles
- Security settings (international payments, night transactions)
- 2FA setup/disable
- Change password form

**Step 6: Update Login page**

- 2FA code input step after successful email/password
- "Forgot password" link

**Step 7: Update Overview page**

- Multi-account display
- Statistics charts (category breakdown, monthly trends)
- Enhanced transaction history with filters

**Step 8: Commit**

```bash
git commit -m "feat(web): integrate all new features into Blazor frontend"
```

---

## Task 13: Docker & Infrastructure Updates

**Files:**
- Modify: `docker-compose.yml` (add cards-api, notifications-api)
- Modify: `src/FairBank.ApiGateway/appsettings.json` (YARP routes)
- Modify: `src/FairBank.ApiGateway/appsettings.Development.json` (local ports)
- Create: `src/Services/Cards/FairBank.Cards.Api/Dockerfile`
- Create: `src/Services/Notifications/FairBank.Notifications.Api/Dockerfile`
- Modify: `FairBank.slnx` (add new projects)

**Step 1: Add projects to solution**

**Step 2: Create Dockerfiles following existing pattern**

**Step 3: Update docker-compose.yml**

**Step 4: Update YARP gateway config**

Cards routes, Notifications routes, Notification Hub SignalR route.

**Step 5: Test full docker-compose up**

```bash
cd /home/kamil/Job/fai/CSAD && docker compose up --build
```

**Step 6: Commit**

```bash
git commit -m "infra: add Cards and Notifications services to Docker and API Gateway"
```

---

## Execution Order & Dependencies

```
Task 1  (Registration KYC)     → no dependencies
Task 2  (Email/Password)       → depends on Task 1 (User entity changes)
Task 3  (2FA)                  → depends on Task 2 (login flow changes)
Task 4  (Devices)              → depends on Task 2 (login flow changes)
Task 5  (Account mgmt)         → no dependencies
Task 6  (Cards service)        → depends on Task 13 (Docker/Gateway)
Task 7  (Limits)               → depends on Task 5 (Account changes)
Task 8  (Statistics)           → no dependencies
Task 9  (Notifications)        → depends on Task 13 (Docker/Gateway)
Task 10 (QR Payments)          → no dependencies
Task 11 (Chat improvements)    → no dependencies
Task 12 (Frontend)             → depends on ALL backend tasks
Task 13 (Docker/Infra)         → no dependencies, do EARLY
```

**Recommended execution order:**
1. Task 13 (Infra) – unblocks 6, 9
2. Task 1 (Registration)
3. Task 2 (Email/Password)
4. Tasks 3, 4, 5, 6, 7, 8, 9, 10, 11 (parallel where possible)
5. Task 12 (Frontend integration – last)
