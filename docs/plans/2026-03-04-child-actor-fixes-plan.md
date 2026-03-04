# Child Actor Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix all broken/missing "Child" actor features: spending limit enforcement in payments, parent approval workflow UI, child account management frontend, and in-app notifications.

**Architecture:** Extend existing Payments service with spending limit checks and PendingTransaction integration. Add Notification entity to Identity service (EF Core). Build new Blazor pages for child management and approval workflow. Add YARP route for notifications.

**Tech Stack:** .NET 10, EF Core, Marten (event sourcing), Blazor WASM, MediatR, FluentValidation, YARP, xUnit + FluentAssertions + NSubstitute

---

## Task 1: Add PendingApproval status to PaymentStatus enum

**Files:**
- Modify: `src/Services/Payments/FairBank.Payments.Domain/Enums/PaymentStatus.cs`

**Step 1: Add PendingApproval value**

```csharp
namespace FairBank.Payments.Domain.Enums;

public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Cancelled = 3,
    PendingApproval = 4
}
```

**Step 2: Commit**

```bash
cd /home/kamil/Job/fai/CSAD
git add src/Services/Payments/FairBank.Payments.Domain/Enums/PaymentStatus.cs
git commit -m "feat: add PendingApproval status to PaymentStatus enum"
```

---

## Task 2: Extend IAccountsServiceClient with limit and pending transaction methods

**Files:**
- Modify: `src/Services/Payments/FairBank.Payments.Application/Ports/IAccountsServiceClient.cs`
- Modify: `src/Services/Payments/FairBank.Payments.Infrastructure/HttpClients/AccountsServiceHttpClient.cs`

**Step 1: Add SpendingLimitInfo record and new methods to interface**

In `IAccountsServiceClient.cs`, add after `AccountInfo` record:

```csharp
public sealed record SpendingLimitInfo(bool RequiresApproval, decimal? ApprovalThreshold, string? Currency);
public sealed record PendingTransactionInfo(Guid Id, string Status);
```

Add to the interface:

```csharp
Task<SpendingLimitInfo?> GetSpendingLimitAsync(Guid accountId, CancellationToken ct = default);
Task<PendingTransactionInfo?> CreatePendingTransactionAsync(Guid accountId, decimal amount, string currency, string description, Guid requestedBy, CancellationToken ct = default);
```

**Step 2: Add new endpoint to Accounts API for limits info**

In `src/Services/Accounts/FairBank.Accounts.Api/Endpoints/AccountEndpoints.cs`, add a new GET endpoint inside the accounts group (after the existing `limits` POST endpoint around line 82):

```csharp
accounts.MapGet("/{id:guid}/limits", async (Guid id, ISender sender) =>
{
    var account = await sender.Send(new GetAccountByIdQuery(id));
    if (account is null) return Results.NotFound();
    return Results.Ok(new { account.RequiresApproval, account.ApprovalThreshold, account.SpendingLimit });
}).WithName("GetSpendingLimit");
```

This requires extending `AccountResponse` DTO. In `src/Services/Accounts/FairBank.Accounts.Application/DTOs/AccountResponse.cs`:

```csharp
public sealed record AccountResponse(
    Guid Id,
    Guid OwnerId,
    string AccountNumber,
    decimal Balance,
    Currency Currency,
    bool IsActive,
    DateTime CreatedAt,
    bool RequiresApproval = false,
    decimal? ApprovalThreshold = null,
    decimal? SpendingLimit = null);
```

Update all places that construct `AccountResponse` to include the new fields. In `SetSpendingLimitCommandHandler`, `CreateAccountCommandHandler`, `DepositMoneyCommandHandler`, `WithdrawMoneyCommandHandler`, and `GetAccountByIdQueryHandler` — add `account.RequiresApproval`, `account.ApprovalThreshold?.Amount`, `account.SpendingLimit?.Amount` to the constructor calls.

**Step 3: Add new endpoint to Accounts API for creating pending transactions**

In `AccountEndpoints.cs`, add inside the pending group:

```csharp
pending.MapPost("/", async (CreatePendingTransactionRequest req, ISender sender) =>
{
    var result = await sender.Send(new CreatePendingTransactionCommand(
        req.AccountId, req.Amount, req.Currency, req.Description, req.RequestedBy));
    return Results.Created($"/api/v1/accounts/pending/{result.Id}", result);
}).WithName("CreatePendingTransaction");
```

Create `CreatePendingTransactionRequest` record:

```csharp
public sealed record CreatePendingTransactionRequest(Guid AccountId, decimal Amount, string Currency, string Description, Guid RequestedBy);
```

Create `CreatePendingTransactionCommand` and handler in `src/Services/Accounts/FairBank.Accounts.Application/Commands/CreatePendingTransaction/`:

`CreatePendingTransactionCommand.cs`:
```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreatePendingTransaction;

public sealed record CreatePendingTransactionCommand(
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    string Description,
    Guid RequestedBy) : IRequest<PendingTransactionResponse>;
```

`CreatePendingTransactionCommandHandler.cs`:
```csharp
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreatePendingTransaction;

public sealed class CreatePendingTransactionCommandHandler(IPendingTransactionStore pendingStore)
    : IRequestHandler<CreatePendingTransactionCommand, PendingTransactionResponse>
{
    public async Task<PendingTransactionResponse> Handle(CreatePendingTransactionCommand request, CancellationToken ct)
    {
        var tx = PendingTransaction.Create(
            request.AccountId,
            Money.Create(request.Amount, request.Currency),
            request.Description,
            request.RequestedBy);

        await pendingStore.StartStreamAsync(tx, ct);

        return new PendingTransactionResponse(
            tx.Id, tx.AccountId, tx.Amount.Amount, tx.Amount.Currency,
            tx.Description, tx.RequestedBy, tx.Status, tx.CreatedAt, tx.ResolvedAt);
    }
}
```

**Step 4: Implement HTTP client methods**

In `AccountsServiceHttpClient.cs`, add:

```csharp
public async Task<SpendingLimitInfo?> GetSpendingLimitAsync(Guid accountId, CancellationToken ct = default)
{
    var response = await httpClient.GetAsync($"api/v1/accounts/{accountId}/limits", ct);
    if (!response.IsSuccessStatusCode) return null;

    var dto = await response.Content.ReadFromJsonAsync<SpendingLimitApiDto>(ct);
    return dto is null ? null : new SpendingLimitInfo(dto.RequiresApproval, dto.ApprovalThreshold, dto.Currency);
}

public async Task<PendingTransactionInfo?> CreatePendingTransactionAsync(
    Guid accountId, decimal amount, string currency, string description, Guid requestedBy, CancellationToken ct = default)
{
    var response = await httpClient.PostAsJsonAsync("api/v1/accounts/pending",
        new { AccountId = accountId, Amount = amount, Currency = currency, Description = description, RequestedBy = requestedBy }, ct);
    if (!response.IsSuccessStatusCode) return null;

    var dto = await response.Content.ReadFromJsonAsync<PendingTransactionApiDto>(ct);
    return dto is null ? null : new PendingTransactionInfo(dto.Id, dto.Status);
}

private sealed record SpendingLimitApiDto(bool RequiresApproval, decimal? ApprovalThreshold, string? Currency, decimal? SpendingLimit);
private sealed record PendingTransactionApiDto(Guid Id, string Status);
```

**Step 5: Run existing tests**

```bash
cd /home/kamil/Job/fai/CSAD
dotnet test tests/FairBank.Accounts.UnitTests/ --verbosity normal
dotnet test tests/FairBank.Payments.UnitTests/ --verbosity normal
```

Expected: All existing tests pass (AccountResponse constructor changes may require updating test code).

**Step 6: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Application/Ports/IAccountsServiceClient.cs
git add src/Services/Payments/FairBank.Payments.Infrastructure/HttpClients/AccountsServiceHttpClient.cs
git add src/Services/Accounts/FairBank.Accounts.Api/Endpoints/AccountEndpoints.cs
git add src/Services/Accounts/FairBank.Accounts.Application/DTOs/AccountResponse.cs
git add src/Services/Accounts/FairBank.Accounts.Application/Commands/CreatePendingTransaction/
git add -A
git commit -m "feat: extend Accounts API with limits info and pending transaction creation endpoints"
```

---

## Task 3: Integrate spending limit check into SendPaymentCommandHandler

**Files:**
- Modify: `src/Services/Payments/FairBank.Payments.Application/Payments/Commands/SendPayment/SendPaymentCommandHandler.cs`
- Create: `tests/FairBank.Payments.UnitTests/Application/SendPaymentWithLimitsTests.cs`

**Step 1: Write the failing test**

Create `tests/FairBank.Payments.UnitTests/Application/SendPaymentWithLimitsTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using FairBank.Payments.Application.Payments.Commands.SendPayment;
using FairBank.Payments.Application.Ports;
using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;

namespace FairBank.Payments.UnitTests.Application;

public class SendPaymentWithLimitsTests
{
    private readonly IPaymentRepository _paymentRepo = Substitute.For<IPaymentRepository>();
    private readonly IAccountsServiceClient _accountsClient = Substitute.For<IAccountsServiceClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_WhenAmountExceedsApprovalThreshold_ShouldCreatePendingTransaction()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var senderAccount = new AccountInfo(senderId, Guid.NewGuid(), "000000-1234567890/8888", 10000m, "CZK", true);

        _accountsClient.GetAccountByIdAsync(senderId, Arg.Any<CancellationToken>()).Returns(senderAccount);
        _accountsClient.GetAccountByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((AccountInfo?)null);
        _accountsClient.GetSpendingLimitAsync(senderId, Arg.Any<CancellationToken>())
            .Returns(new SpendingLimitInfo(true, 500m, "CZK"));
        _accountsClient.CreatePendingTransactionAsync(
            senderId, 1000m, "CZK", Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new PendingTransactionInfo(Guid.NewGuid(), "Pending"));

        var handler = new SendPaymentCommandHandler(_paymentRepo, _accountsClient, _unitOfWork);
        var command = new SendPaymentCommand(senderId, "000000-9999999999/8888", 1000m, "CZK", "Test payment");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("PendingApproval");
        await _accountsClient.Received(1).CreatePendingTransactionAsync(
            senderId, 1000m, "CZK", Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _accountsClient.DidNotReceive().WithdrawAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAmountBelowThreshold_ShouldProcessNormally()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var senderAccount = new AccountInfo(senderId, Guid.NewGuid(), "000000-1234567890/8888", 10000m, "CZK", true);

        _accountsClient.GetAccountByIdAsync(senderId, Arg.Any<CancellationToken>()).Returns(senderAccount);
        _accountsClient.GetAccountByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((AccountInfo?)null);
        _accountsClient.GetSpendingLimitAsync(senderId, Arg.Any<CancellationToken>())
            .Returns(new SpendingLimitInfo(true, 500m, "CZK"));
        _accountsClient.WithdrawAsync(senderId, 200m, "CZK", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new SendPaymentCommandHandler(_paymentRepo, _accountsClient, _unitOfWork);
        var command = new SendPaymentCommand(senderId, "000000-9999999999/8888", 200m, "CZK", "Small payment");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("Completed");
        await _accountsClient.DidNotReceive().CreatePendingTransactionAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoSpendingLimit_ShouldProcessNormally()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var senderAccount = new AccountInfo(senderId, Guid.NewGuid(), "000000-1234567890/8888", 10000m, "CZK", true);

        _accountsClient.GetAccountByIdAsync(senderId, Arg.Any<CancellationToken>()).Returns(senderAccount);
        _accountsClient.GetAccountByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((AccountInfo?)null);
        _accountsClient.GetSpendingLimitAsync(senderId, Arg.Any<CancellationToken>())
            .Returns(new SpendingLimitInfo(false, null, null));
        _accountsClient.WithdrawAsync(senderId, 5000m, "CZK", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new SendPaymentCommandHandler(_paymentRepo, _accountsClient, _unitOfWork);
        var command = new SendPaymentCommand(senderId, "000000-9999999999/8888", 5000m, "CZK", "Big payment");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("Completed");
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/FairBank.Payments.UnitTests/ --filter "SendPaymentWithLimitsTests" --verbosity normal
```

Expected: FAIL — `GetSpendingLimitAsync` not yet called in handler.

**Step 3: Modify SendPaymentCommandHandler**

Replace the `Handle` method in `SendPaymentCommandHandler.cs`. Insert the spending limit check after step 3 (balance check) and before step 4 (payment type):

After line 31 (`throw new InvalidOperationException("Insufficient funds.");`), add:

```csharp
        // 3b. Check spending limits (child account protection)
        var limits = await accountsClient.GetSpendingLimitAsync(request.SenderAccountId, ct);
        if (limits is { RequiresApproval: true, ApprovalThreshold: not null }
            && request.Amount > limits.ApprovalThreshold.Value)
        {
            // Create pending transaction for parent approval
            var pending = await accountsClient.CreatePendingTransactionAsync(
                request.SenderAccountId, request.Amount, request.Currency,
                $"Platba → {request.RecipientAccountNumber}: {request.Description ?? ""}".Trim(),
                senderAccount.OwnerId, ct);

            if (pending is null)
                throw new InvalidOperationException("Failed to create pending transaction.");

            // Create payment record with PendingApproval status
            var currency3b = Enum.Parse<Currency>(request.Currency, true);
            var pendingPayment = Payment.Create(
                senderAccountId: request.SenderAccountId,
                senderAccountNumber: senderAccount.AccountNumber,
                recipientAccountNumber: request.RecipientAccountNumber,
                amount: request.Amount,
                currency: currency3b,
                type: request.IsInstant ? PaymentType.Instant : PaymentType.Standard,
                description: request.Description);

            await paymentRepository.AddAsync(pendingPayment, ct);
            await unitOfWork.SaveChangesAsync(ct);

            // We need a method to set PendingApproval status
            // Add to Payment entity: MarkPendingApproval()
            return MapToResponse(pendingPayment);
        }
```

**Step 4: Add MarkPendingApproval to Payment entity**

In `src/Services/Payments/FairBank.Payments.Domain/Entities/Payment.cs`, add method:

```csharp
public void MarkPendingApproval()
{
    if (Status != PaymentStatus.Pending)
        throw new InvalidOperationException($"Cannot mark as pending approval from {Status}.");
    Status = PaymentStatus.PendingApproval;
}
```

Then update the handler to call `pendingPayment.MarkPendingApproval()` before `AddAsync`.

**Step 5: Run tests**

```bash
dotnet test tests/FairBank.Payments.UnitTests/ --verbosity normal
```

Expected: All tests pass including new ones.

**Step 6: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Application/Payments/Commands/SendPayment/SendPaymentCommandHandler.cs
git add src/Services/Payments/FairBank.Payments.Domain/Entities/Payment.cs
git add tests/FairBank.Payments.UnitTests/Application/SendPaymentWithLimitsTests.cs
git commit -m "feat: integrate spending limit checks into payment flow with PendingApproval status"
```

---

## Task 4: Add Notification entity and repository to Identity service

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Domain/Entities/Notification.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/Enums/NotificationType.cs`
- Create: `src/Services/Identity/FairBank.Identity.Domain/Ports/INotificationRepository.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`
- Create: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/NotificationRepository.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/IdentityDbContext.cs`
- Create: `tests/FairBank.Identity.UnitTests/Domain/NotificationTests.cs`

**Step 1: Write the failing domain test**

Create `tests/FairBank.Identity.UnitTests/Domain/NotificationTests.cs`:

```csharp
using FluentAssertions;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;

namespace FairBank.Identity.UnitTests.Domain;

public class NotificationTests
{
    [Fact]
    public void Create_ShouldInitializeWithUnreadStatus()
    {
        var userId = Guid.NewGuid();
        var notification = Notification.Create(
            userId, NotificationType.TransactionPending,
            "Nová platba dítěte", "Jan zaplatil 200 CZK");

        notification.Id.Should().NotBe(Guid.Empty);
        notification.UserId.Should().Be(userId);
        notification.Type.Should().Be(NotificationType.TransactionPending);
        notification.Title.Should().Be("Nová platba dítěte");
        notification.Message.Should().Be("Jan zaplatil 200 CZK");
        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public void MarkAsRead_ShouldSetIsReadTrue()
    {
        var notification = Notification.Create(
            Guid.NewGuid(), NotificationType.TransactionCompleted,
            "Test", "Test message");

        notification.MarkAsRead();

        notification.IsRead.Should().BeTrue();
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/FairBank.Identity.UnitTests/ --filter "NotificationTests" --verbosity normal
```

Expected: FAIL — `Notification` class doesn't exist.

**Step 3: Create NotificationType enum**

Create `src/Services/Identity/FairBank.Identity.Domain/Enums/NotificationType.cs`:

```csharp
namespace FairBank.Identity.Domain.Enums;

public enum NotificationType
{
    TransactionCompleted = 0,
    TransactionPending = 1,
    TransactionApproved = 2,
    TransactionRejected = 3
}
```

**Step 4: Create Notification entity**

Create `src/Services/Identity/FairBank.Identity.Domain/Entities/Notification.cs`:

```csharp
using FairBank.Identity.Domain.Enums;
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Entities;

public sealed class Notification : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public bool IsRead { get; private set; }
    public Guid? RelatedEntityId { get; private set; }
    public string? RelatedEntityType { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            IsRead = false,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
```

**Step 5: Run tests**

```bash
dotnet test tests/FairBank.Identity.UnitTests/ --filter "NotificationTests" --verbosity normal
```

Expected: PASS

**Step 6: Create repository interface, implementation, and EF configuration**

Create `src/Services/Identity/FairBank.Identity.Domain/Ports/INotificationRepository.cs`:

```csharp
namespace FairBank.Identity.Domain.Ports;

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
```

(Add missing using: `using FairBank.Identity.Domain.Entities;`)

Create `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`:

```csharp
using FairBank.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Identity.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.UserId).IsRequired();
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.IsRead).IsRequired().HasDefaultValue(false);
        builder.Property(n => n.RelatedEntityId);
        builder.Property(n => n.RelatedEntityType).HasMaxLength(50);
        builder.Property(n => n.CreatedAt).IsRequired();

        builder.HasIndex(n => new { n.UserId, n.IsRead });
        builder.HasIndex(n => n.CreatedAt);
    }
}
```

Create `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/NotificationRepository.cs`:

```csharp
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Identity.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(IdentityDbContext context) : INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false, CancellationToken ct = default)
    {
        var query = context.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        return await query.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        await context.Notifications.AddAsync(notification, ct);
    }

    public async Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        context.Notifications.Update(notification);
        await Task.CompletedTask;
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        await context.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
```

Add `DbSet<Notification>` to `IdentityDbContext.cs`:

```csharp
public DbSet<Notification> Notifications => Set<Notification>();
```

Register in `DependencyInjection.cs`:

```csharp
services.AddScoped<INotificationRepository, NotificationRepository>();
```

**Step 7: Generate EF migration**

```bash
cd /home/kamil/Job/fai/CSAD
dotnet ef migrations add AddNotifications --project src/Services/Identity/FairBank.Identity.Infrastructure --startup-project src/Services/Identity/FairBank.Identity.Api
```

**Step 8: Run all Identity tests**

```bash
dotnet test tests/FairBank.Identity.UnitTests/ --verbosity normal
```

Expected: All tests pass.

**Step 9: Commit**

```bash
git add src/Services/Identity/FairBank.Identity.Domain/Entities/Notification.cs
git add src/Services/Identity/FairBank.Identity.Domain/Enums/NotificationType.cs
git add src/Services/Identity/FairBank.Identity.Domain/Ports/INotificationRepository.cs
git add src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs
git add src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/NotificationRepository.cs
git add src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/IdentityDbContext.cs
git add src/Services/Identity/FairBank.Identity.Infrastructure/DependencyInjection.cs
git add src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Migrations/
git add tests/FairBank.Identity.UnitTests/Domain/NotificationTests.cs
git commit -m "feat: add Notification entity and repository to Identity service"
```

---

## Task 5: Add notification CQRS commands/queries and API endpoints

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Application/Notifications/DTOs/NotificationResponse.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Notifications/Commands/CreateNotification/CreateNotificationCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Notifications/Commands/CreateNotification/CreateNotificationCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Notifications/Commands/MarkNotificationRead/MarkNotificationReadCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Notifications/Commands/MarkNotificationRead/MarkNotificationReadCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Notifications/Commands/MarkAllRead/MarkAllReadCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Notifications/Commands/MarkAllRead/MarkAllReadCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Notifications/Queries/GetNotifications/GetNotificationsQuery.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Notifications/Queries/GetNotifications/GetNotificationsQueryHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Notifications/Queries/GetUnreadCount/GetUnreadCountQuery.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Notifications/Queries/GetUnreadCount/GetUnreadCountQueryHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Api/Endpoints/NotificationEndpoints.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Program.cs` (map new endpoints)

**Step 1: Create DTO**

`NotificationResponse.cs`:
```csharp
namespace FairBank.Identity.Application.Notifications.DTOs;

public sealed record NotificationResponse(
    Guid Id,
    Guid UserId,
    string Type,
    string Title,
    string Message,
    bool IsRead,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    DateTime CreatedAt);
```

**Step 2: Create Commands and Handlers**

`CreateNotificationCommand.cs`:
```csharp
using FairBank.Identity.Application.Notifications.DTOs;
using FairBank.Identity.Domain.Enums;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Commands.CreateNotification;

public sealed record CreateNotificationCommand(
    Guid UserId,
    NotificationType Type,
    string Title,
    string Message,
    Guid? RelatedEntityId = null,
    string? RelatedEntityType = null) : IRequest<NotificationResponse>;
```

`CreateNotificationCommandHandler.cs`:
```csharp
using FairBank.Identity.Application.Notifications.DTOs;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Commands.CreateNotification;

public sealed class CreateNotificationCommandHandler(
    INotificationRepository notificationRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateNotificationCommand, NotificationResponse>
{
    public async Task<NotificationResponse> Handle(CreateNotificationCommand request, CancellationToken ct)
    {
        var notification = Notification.Create(
            request.UserId, request.Type, request.Title, request.Message,
            request.RelatedEntityId, request.RelatedEntityType);

        await notificationRepo.AddAsync(notification, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new NotificationResponse(
            notification.Id, notification.UserId, notification.Type.ToString(),
            notification.Title, notification.Message, notification.IsRead,
            notification.RelatedEntityId, notification.RelatedEntityType, notification.CreatedAt);
    }
}
```

`MarkNotificationReadCommand.cs`:
```csharp
using MediatR;

namespace FairBank.Identity.Application.Notifications.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest;
```

`MarkNotificationReadCommandHandler.cs`:
```csharp
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandHandler(
    INotificationRepository notificationRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await notificationRepo.GetByIdAsync(request.NotificationId, ct)
            ?? throw new InvalidOperationException("Notification not found.");
        notification.MarkAsRead();
        await notificationRepo.UpdateAsync(notification, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
```

`MarkAllReadCommand.cs`:
```csharp
using MediatR;

namespace FairBank.Identity.Application.Notifications.Commands.MarkAllRead;

public sealed record MarkAllReadCommand(Guid UserId) : IRequest;
```

`MarkAllReadCommandHandler.cs`:
```csharp
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Commands.MarkAllRead;

public sealed class MarkAllReadCommandHandler(INotificationRepository notificationRepo)
    : IRequestHandler<MarkAllReadCommand>
{
    public async Task Handle(MarkAllReadCommand request, CancellationToken ct)
    {
        await notificationRepo.MarkAllReadAsync(request.UserId, ct);
    }
}
```

**Step 3: Create Queries and Handlers**

`GetNotificationsQuery.cs`:
```csharp
using FairBank.Identity.Application.Notifications.DTOs;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery(Guid UserId, bool UnreadOnly = false) : IRequest<IReadOnlyList<NotificationResponse>>;
```

`GetNotificationsQueryHandler.cs`:
```csharp
using FairBank.Identity.Application.Notifications.DTOs;
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsQueryHandler(INotificationRepository notificationRepo)
    : IRequestHandler<GetNotificationsQuery, IReadOnlyList<NotificationResponse>>
{
    public async Task<IReadOnlyList<NotificationResponse>> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var notifications = await notificationRepo.GetByUserIdAsync(request.UserId, request.UnreadOnly, ct);
        return notifications.Select(n => new NotificationResponse(
            n.Id, n.UserId, n.Type.ToString(), n.Title, n.Message, n.IsRead,
            n.RelatedEntityId, n.RelatedEntityType, n.CreatedAt)).ToList();
    }
}
```

`GetUnreadCountQuery.cs`:
```csharp
using MediatR;

namespace FairBank.Identity.Application.Notifications.Queries.GetUnreadCount;

public sealed record GetUnreadCountQuery(Guid UserId) : IRequest<int>;
```

`GetUnreadCountQueryHandler.cs`:
```csharp
using FairBank.Identity.Domain.Ports;
using MediatR;

namespace FairBank.Identity.Application.Notifications.Queries.GetUnreadCount;

public sealed class GetUnreadCountQueryHandler(INotificationRepository notificationRepo)
    : IRequestHandler<GetUnreadCountQuery, int>
{
    public async Task<int> Handle(GetUnreadCountQuery request, CancellationToken ct)
    {
        return await notificationRepo.GetUnreadCountAsync(request.UserId, ct);
    }
}
```

**Step 4: Create NotificationEndpoints**

Create `src/Services/Identity/FairBank.Identity.Api/Endpoints/NotificationEndpoints.cs`:

```csharp
using FairBank.Identity.Application.Notifications.Commands.CreateNotification;
using FairBank.Identity.Application.Notifications.Commands.MarkAllRead;
using FairBank.Identity.Application.Notifications.Commands.MarkNotificationRead;
using FairBank.Identity.Application.Notifications.Queries.GetNotifications;
using FairBank.Identity.Application.Notifications.Queries.GetUnreadCount;
using FairBank.Identity.Domain.Enums;
using MediatR;

namespace FairBank.Identity.Api.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications").WithTags("Notifications");

        group.MapGet("/", async (Guid userId, bool? unreadOnly, ISender sender) =>
        {
            var result = await sender.Send(new GetNotificationsQuery(userId, unreadOnly ?? false));
            return Results.Ok(result);
        }).WithName("GetNotifications");

        group.MapGet("/count", async (Guid userId, ISender sender) =>
        {
            var count = await sender.Send(new GetUnreadCountQuery(userId));
            return Results.Ok(new { count });
        }).WithName("GetUnreadCount");

        group.MapPost("/", async (CreateNotificationRequest req, ISender sender) =>
        {
            if (!Enum.TryParse<NotificationType>(req.Type, true, out var type))
                return Results.BadRequest("Invalid notification type.");

            var result = await sender.Send(new CreateNotificationCommand(
                req.UserId, type, req.Title, req.Message, req.RelatedEntityId, req.RelatedEntityType));
            return Results.Created($"/api/v1/notifications/{result.Id}", result);
        }).WithName("CreateNotification");

        group.MapPost("/{id:guid}/read", async (Guid id, ISender sender) =>
        {
            await sender.Send(new MarkNotificationReadCommand(id));
            return Results.Ok();
        }).WithName("MarkNotificationRead");

        group.MapPost("/read-all", async (Guid userId, ISender sender) =>
        {
            await sender.Send(new MarkAllReadCommand(userId));
            return Results.Ok();
        }).WithName("MarkAllNotificationsRead");

        return group;
    }
}

public sealed record CreateNotificationRequest(
    Guid UserId, string Type, string Title, string Message,
    Guid? RelatedEntityId = null, string? RelatedEntityType = null);
```

**Step 5: Register endpoints in Program.cs**

In `src/Services/Identity/FairBank.Identity.Api/Program.cs`, add:

```csharp
app.MapNotificationEndpoints();
```

Add the using statement at the top.

**Step 6: Add YARP route for notifications**

In `src/FairBank.ApiGateway/appsettings.json`, add to the `Routes` object:

```json
"notifications-route": {
    "ClusterId": "identity-cluster",
    "Match": {
        "Path": "/api/v1/notifications/{**catch-all}"
    }
}
```

**Step 7: Run all Identity tests**

```bash
dotnet test tests/FairBank.Identity.UnitTests/ --verbosity normal
```

Expected: All pass.

**Step 8: Commit**

```bash
git add src/Services/Identity/FairBank.Identity.Application/Notifications/
git add src/Services/Identity/FairBank.Identity.Api/Endpoints/NotificationEndpoints.cs
git add src/Services/Identity/FairBank.Identity.Api/Program.cs
git add src/FairBank.ApiGateway/appsettings.json
git commit -m "feat: add notification CQRS commands/queries and API endpoints with YARP route"
```

---

## Task 6: Add notification HTTP client to Payments service

**Files:**
- Create: `src/Services/Payments/FairBank.Payments.Application/Ports/INotificationClient.cs`
- Create: `src/Services/Payments/FairBank.Payments.Infrastructure/HttpClients/NotificationHttpClient.cs`
- Modify: `src/Services/Payments/FairBank.Payments.Infrastructure/DependencyInjection.cs`
- Modify: `src/Services/Payments/FairBank.Payments.Api/Program.cs`
- Modify: `docker-compose.yml` (add Identity API URL env var to payments-api)

**Step 1: Create notification port**

`INotificationClient.cs`:
```csharp
namespace FairBank.Payments.Application.Ports;

public interface INotificationClient
{
    Task SendAsync(Guid userId, string type, string title, string message,
        Guid? relatedEntityId = null, string? relatedEntityType = null, CancellationToken ct = default);
}
```

**Step 2: Create HTTP client implementation**

`NotificationHttpClient.cs`:
```csharp
using System.Net.Http.Json;
using FairBank.Payments.Application.Ports;

namespace FairBank.Payments.Infrastructure.HttpClients;

public sealed class NotificationHttpClient(HttpClient httpClient) : INotificationClient
{
    public async Task SendAsync(Guid userId, string type, string title, string message,
        Guid? relatedEntityId = null, string? relatedEntityType = null, CancellationToken ct = default)
    {
        try
        {
            await httpClient.PostAsJsonAsync("api/v1/notifications",
                new { UserId = userId, Type = type, Title = title, Message = message,
                      RelatedEntityId = relatedEntityId, RelatedEntityType = relatedEntityType }, ct);
        }
        catch
        {
            // Fire and forget — notification failure should not block payment
        }
    }
}
```

**Step 3: Register in DI**

In `src/Services/Payments/FairBank.Payments.Infrastructure/DependencyInjection.cs`, add parameter `string identityApiBaseUrl` and register:

```csharp
services.AddHttpClient<INotificationClient, NotificationHttpClient>(client =>
{
    client.BaseAddress = new Uri(identityApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
```

In `src/Services/Payments/FairBank.Payments.Api/Program.cs`, read the new config and pass it:

```csharp
var identityApiUrl = builder.Configuration["Services:IdentityApi"] ?? "http://identity-api:8080";
builder.Services.AddPaymentsInfrastructure(connectionString, accountsApiUrl, identityApiUrl);
```

**Step 4: Inject into SendPaymentCommandHandler and send notifications**

Add `INotificationClient notificationClient` to the constructor. After creating a PendingApproval payment, send notification to parent. This requires knowing the parent's userId — the `senderAccount.OwnerId` is the child, so we need to look up the parent. For now, we can store the notification for the child's owner and let the frontend resolve parent from the children relationship.

Actually, simpler approach: the Accounts service returns `OwnerId` for the account. The Payments service can call Identity to get the parent. But that's too complex. Instead, **the notification should be created by the Accounts service** when a PendingTransaction is created, since Accounts already knows the account owner and can be extended to call Identity for parent lookup.

**Alternative simpler approach:** Have the `CreatePendingTransactionCommandHandler` in Accounts service call the Identity API to send the notification. This keeps Payments service simple.

For this plan, we'll add notification sending to the `SendPaymentCommandHandler` in Payments — it already has the `senderAccount.OwnerId` (the child userId). The frontend can then look up the parent and show notifications. The notification `UserId` should be the **parent**, which we need to fetch.

**Simplest approach:** Add a method to `IAccountsServiceClient` to get the account owner's parent:

Actually, the cleanest solution: extend `AccountInfo` to include `ParentId` if the owner is a child. This requires the Accounts service to either store parentId or the Identity API to provide it.

**Pragmatic solution:** The Payments service sends the notification with the child's userId. The frontend, when a parent logs in, queries notifications for all their children's userIds. OR: we add a `parentId` field to the notification so it's targeted at the parent.

**Even simpler:** The `SendPaymentCommandHandler` doesn't know the parentId. Instead, add an `IIdentityClient` to Payments that can look up the parent of a user. One HTTP call.

Create `src/Services/Payments/FairBank.Payments.Application/Ports/IIdentityClient.cs`:

```csharp
namespace FairBank.Payments.Application.Ports;

public interface IIdentityClient
{
    Task<UserInfo?> GetUserAsync(Guid userId, CancellationToken ct = default);
}

public sealed record UserInfo(Guid Id, string FirstName, string LastName, string Role, Guid? ParentId);
```

Create `src/Services/Payments/FairBank.Payments.Infrastructure/HttpClients/IdentityHttpClient.cs`:

```csharp
using System.Net.Http.Json;
using FairBank.Payments.Application.Ports;

namespace FairBank.Payments.Infrastructure.HttpClients;

public sealed class IdentityHttpClient(HttpClient httpClient) : IIdentityClient
{
    public async Task<UserInfo?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/v1/users/{userId}", ct);
        if (!response.IsSuccessStatusCode) return null;
        var dto = await response.Content.ReadFromJsonAsync<UserApiDto>(ct);
        return dto is null ? null : new UserInfo(dto.Id, dto.FirstName, dto.LastName, dto.Role,
            dto.ParentId == Guid.Empty ? null : dto.ParentId);
    }

    private sealed record UserApiDto(Guid Id, string FirstName, string LastName, string Role, Guid? ParentId);
}
```

Register both in DI:

```csharp
services.AddHttpClient<IIdentityClient, IdentityHttpClient>(client =>
{
    client.BaseAddress = new Uri(identityApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
```

**Step 5: Update SendPaymentCommandHandler to send notification**

Add `INotificationClient notificationClient` and `IIdentityClient identityClient` to constructor. After the PendingApproval branch, add:

```csharp
// Send notification to parent
var childUser = await identityClient.GetUserAsync(senderAccount.OwnerId, ct);
if (childUser?.ParentId is not null)
{
    await notificationClient.SendAsync(
        childUser.ParentId.Value,
        "TransactionPending",
        $"Platba čeká na schválení",
        $"{childUser.FirstName} chce zaplatit {request.Amount} {request.Currency} → {request.RecipientAccountNumber}",
        pending.Id, "PendingTransaction", ct);
}
```

**Step 6: Update docker-compose.yml**

Add environment variable to `payments-api` service:

```yaml
Services__IdentityApi: http://identity-api:8080
```

**Step 7: Run tests**

```bash
dotnet test tests/FairBank.Payments.UnitTests/ --verbosity normal
```

Expected: Existing tests may fail due to new constructor parameters. Update mocks in existing tests to include new `INotificationClient` and `IIdentityClient` substitutes.

**Step 8: Commit**

```bash
git add src/Services/Payments/FairBank.Payments.Application/Ports/INotificationClient.cs
git add src/Services/Payments/FairBank.Payments.Application/Ports/IIdentityClient.cs
git add src/Services/Payments/FairBank.Payments.Infrastructure/HttpClients/NotificationHttpClient.cs
git add src/Services/Payments/FairBank.Payments.Infrastructure/HttpClients/IdentityHttpClient.cs
git add src/Services/Payments/FairBank.Payments.Infrastructure/DependencyInjection.cs
git add src/Services/Payments/FairBank.Payments.Api/Program.cs
git add src/Services/Payments/FairBank.Payments.Application/Payments/Commands/SendPayment/SendPaymentCommandHandler.cs
git add docker-compose.yml
git commit -m "feat: add notification and identity clients to Payments service for parent notifications"
```

---

## Task 7: Add frontend notification model and API client methods

**Files:**
- Create: `src/FairBank.Web.Shared/Models/NotificationDto.cs`
- Modify: `src/FairBank.Web.Shared/Services/IFairBankApi.cs`
- Modify: `src/FairBank.Web.Shared/Services/FairBankApiClient.cs`

**Step 1: Create NotificationDto**

```csharp
namespace FairBank.Web.Shared.Models;

public sealed record NotificationDto(
    Guid Id,
    Guid UserId,
    string Type,
    string Title,
    string Message,
    bool IsRead,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    DateTime CreatedAt);
```

**Step 2: Add methods to IFairBankApi**

```csharp
// Notifications
Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, bool unreadOnly = false);
Task<int> GetUnreadNotificationCountAsync(Guid userId);
Task MarkNotificationReadAsync(Guid notificationId);
Task MarkAllNotificationsReadAsync(Guid userId);
```

**Step 3: Implement in FairBankApiClient**

```csharp
public async Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, bool unreadOnly = false)
{
    var url = $"api/v1/notifications?userId={userId}&unreadOnly={unreadOnly}";
    return await _http.GetFromJsonAsync<List<NotificationDto>>(url) ?? [];
}

public async Task<int> GetUnreadNotificationCountAsync(Guid userId)
{
    var result = await _http.GetFromJsonAsync<UnreadCountResponse>($"api/v1/notifications/count?userId={userId}");
    return result?.Count ?? 0;
}

public async Task MarkNotificationReadAsync(Guid notificationId)
{
    await _http.PostAsync($"api/v1/notifications/{notificationId}/read", null);
}

public async Task MarkAllNotificationsReadAsync(Guid userId)
{
    await _http.PostAsync($"api/v1/notifications/read-all?userId={userId}", null);
}

private sealed record UnreadCountResponse(int Count);
```

**Step 4: Commit**

```bash
git add src/FairBank.Web.Shared/Models/NotificationDto.cs
git add src/FairBank.Web.Shared/Services/IFairBankApi.cs
git add src/FairBank.Web.Shared/Services/FairBankApiClient.cs
git commit -m "feat: add notification API client methods to frontend shared"
```

---

## Task 8: Build notification bell component in MainLayout

**Files:**
- Modify: `src/FairBank.Web/Layout/MainLayout.razor`

**Step 1: Add notification bell with polling**

Replace the static bell button in `MainLayout.razor` with a functional component:

```razor
<div class="topbar-actions">
    <div class="notification-wrapper" @onclick="ToggleNotifications">
        <button class="topbar-btn">
            <VbIcon Name="bell" Size="sm" />
            @if (_unreadCount > 0)
            {
                <span class="notification-badge">@(_unreadCount > 9 ? "9+" : _unreadCount.ToString())</span>
            }
        </button>
        @if (_showNotifications)
        {
            <div class="notification-dropdown">
                <div class="notification-header">
                    <span>Notifikace</span>
                    @if (_unreadCount > 0)
                    {
                        <button class="mark-all-btn" @onclick="MarkAllRead">Označit vše</button>
                    }
                </div>
                @if (_notifications.Count == 0)
                {
                    <div class="notification-empty">Žádné notifikace</div>
                }
                @foreach (var n in _notifications.Take(10))
                {
                    <div class="notification-item @(n.IsRead ? "" : "unread")" @onclick="() => MarkRead(n.Id)">
                        <div class="notification-title">@n.Title</div>
                        <div class="notification-message">@n.Message</div>
                        <div class="notification-time">@n.CreatedAt.ToString("dd.MM. HH:mm")</div>
                    </div>
                }
            </div>
        }
    </div>
    <button class="topbar-btn theme-toggle" @onclick="ToggleTheme">
        <VbIcon Name="@(Theme.IsDarkMode ? "sun" : "moon")" Size="sm" />
    </button>
</div>
```

Add to `@code` block:

```csharp
@inject IFairBankApi Api

private int _unreadCount;
private bool _showNotifications;
private List<NotificationDto> _notifications = [];
private Timer? _pollTimer;

protected override async Task OnInitializedAsync()
{
    if (Auth.CurrentSession is not null)
    {
        await LoadNotifications();
        _pollTimer = new Timer(async _ => await InvokeAsync(async () =>
        {
            await LoadNotifications();
            StateHasChanged();
        }), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }
}

private async Task LoadNotifications()
{
    if (Auth.CurrentSession is null) return;
    try
    {
        _unreadCount = await Api.GetUnreadNotificationCountAsync(Auth.CurrentSession.UserId);
        _notifications = await Api.GetNotificationsAsync(Auth.CurrentSession.UserId);
    }
    catch { /* ignore polling errors */ }
}

private void ToggleNotifications() => _showNotifications = !_showNotifications;

private async Task MarkRead(Guid id)
{
    await Api.MarkNotificationReadAsync(id);
    await LoadNotifications();
}

private async Task MarkAllRead()
{
    if (Auth.CurrentSession is null) return;
    await Api.MarkAllNotificationsReadAsync(Auth.CurrentSession.UserId);
    await LoadNotifications();
}
```

Add CSS styles in a `<style>` block for `.notification-wrapper`, `.notification-badge`, `.notification-dropdown`, `.notification-item`, `.unread`, etc. Follow the existing design system CSS variables (`--vb-red`, `--vb-bg-elevated`, `--vb-text-primary`, etc.).

**Step 2: Commit**

```bash
git add src/FairBank.Web/Layout/MainLayout.razor
git commit -m "feat: add functional notification bell with polling to MainLayout"
```

---

## Task 9: Add "Děti" (Children) page for parent (Client role)

**Files:**
- Modify: `src/FairBank.Web/Layout/SideNav.razor` (add "Děti" nav item)
- Create: `src/FairBank.Web/Pages/Children.razor`
- Create: `src/FairBank.Web/Pages/ChildDetail.razor`

**Step 1: Add SideNav item**

In `SideNav.razor`, add after the Produkty conditional block (after line 43):

```razor
@if (Auth.CurrentSession?.Role == "Client")
{
    <NavLink class="side-nav-item" href="deti">
        <span class="side-nav-icon"><VbIcon Name="users" Size="sm" /></span>
        <span class="side-nav-label">Děti</span>
    </NavLink>
}
```

Also hide Spoření, Investice, Kurzy for Child role — wrap lines 25-36 in:

```razor
@if (Auth.CurrentSession?.Role != "Child")
{
    <!-- Spoření, Investice, Kurzy nav items -->
}
```

**Step 2: Create Children.razor (list page)**

Create `src/FairBank.Web/Pages/Children.razor`:

```razor
@page "/deti"
@inject IFairBankApi Api
@inject IAuthService Auth
@inject NavigationManager Navigation

<PageHeader Title="MOJE DĚTI" ShowChip="false">
    <Actions>
        <VbButton Variant="outline" Size="sm" IconName="plus" OnClick="@(() => _showAddForm = true)">
            Přidat dítě
        </VbButton>
    </Actions>
</PageHeader>

<div class="page-content children-page">
    @if (_loading)
    {
        <div class="loading-state">Načítání...</div>
    }
    else if (_children.Count == 0)
    {
        <ContentCard Title="Zatím nemáte žádné dětské účty">
            <ChildContent>
                <p style="color: var(--vb-text-secondary);">Vytvořte dětský účet pro správu kapesného a finanční výchovu.</p>
                <VbButton Variant="primary" OnClick="@(() => _showAddForm = true)">Přidat dítě</VbButton>
            </ChildContent>
        </ContentCard>
    }
    else
    {
        @foreach (var child in _children)
        {
            <div class="child-card" @onclick="() => Navigation.NavigateTo($\"deti/{child.Id}\")">
                <div class="child-avatar">@child.FirstName[0]</div>
                <div class="child-info">
                    <div class="child-name">@child.FirstName @child.LastName</div>
                    <div class="child-email">@child.Email</div>
                </div>
                <VbIcon Name="chevron-right" Size="sm" Color="var(--vb-text-muted)" />
            </div>
        }
    }

    @if (_showAddForm)
    {
        <div class="modal-overlay" @onclick="() => _showAddForm = false">
            <div class="modal-card" @onclick:stopPropagation="true">
                <h3>Nové dítě</h3>
                <div class="form-group">
                    <label>Jméno</label>
                    <input @bind="_newFirstName" placeholder="Jan" />
                </div>
                <div class="form-group">
                    <label>Příjmení</label>
                    <input @bind="_newLastName" placeholder="Novák" />
                </div>
                <div class="form-group">
                    <label>Email</label>
                    <input @bind="_newEmail" type="email" placeholder="jan@example.com" />
                </div>
                <div class="form-group">
                    <label>Heslo</label>
                    <input @bind="_newPassword" type="password" placeholder="••••••••" />
                </div>
                @if (!string.IsNullOrEmpty(_error))
                {
                    <div class="form-error">@_error</div>
                }
                <div class="modal-actions">
                    <VbButton Variant="outline" OnClick="@(() => _showAddForm = false)">Zrušit</VbButton>
                    <VbButton Variant="primary" OnClick="CreateChild">Vytvořit</VbButton>
                </div>
            </div>
        </div>
    }
</div>

@code {
    private List<UserResponse> _children = [];
    private bool _loading = true;
    private bool _showAddForm;
    private string _newFirstName = "";
    private string _newLastName = "";
    private string _newEmail = "";
    private string _newPassword = "";
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        if (Auth.CurrentSession is null) return;
        await LoadChildren();
    }

    private async Task LoadChildren()
    {
        _loading = true;
        try
        {
            _children = await Api.GetChildrenAsync(Auth.CurrentSession!.UserId);
        }
        catch { _children = []; }
        _loading = false;
    }

    private async Task CreateChild()
    {
        _error = null;
        try
        {
            var child = await Api.CreateChildAsync(
                Auth.CurrentSession!.UserId, _newFirstName, _newLastName, _newEmail, _newPassword);

            // Auto-create account for the child
            await Api.CreateAccountAsync(child.Id, "CZK");

            _showAddForm = false;
            _newFirstName = _newLastName = _newEmail = _newPassword = "";
            await LoadChildren();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
    }
}
```

Add `<style>` block with CSS for `.children-page`, `.child-card`, `.child-avatar`, `.child-info`, `.modal-overlay`, `.modal-card`, `.form-group`, etc. Use existing design system variables.

**Step 3: Create ChildDetail.razor**

Create `src/FairBank.Web/Pages/ChildDetail.razor`:

```razor
@page "/deti/{ChildId:guid}"
@inject IFairBankApi Api
@inject IAuthService Auth
@inject NavigationManager Navigation

<PageHeader Title="@_childName" ShowChip="false">
    <Actions>
        <VbButton Variant="outline" Size="sm" OnClick="@(() => Navigation.NavigateTo("deti"))">
            ← Zpět
        </VbButton>
    </Actions>
</PageHeader>

<div class="page-content child-detail-page">
    @if (_loading)
    {
        <div class="loading-state">Načítání...</div>
    }
    else
    {
        @* Account Overview *@
        @if (_account is not null)
        {
            <ContentCard Title="ÚČET">
                <ChildContent>
                    <div class="account-balance">
                        <span class="balance-amount">@_account.Balance.ToString("N2")</span>
                        <span class="balance-currency">@_account.Currency</span>
                    </div>
                    <div class="account-number">@_account.AccountNumber</div>
                </ChildContent>
            </ContentCard>
        }

        @* Spending Limits *@
        <ContentCard Title="LIMITY">
            <ChildContent>
                <div class="form-group">
                    <label>Denní limit (CZK)</label>
                    <input type="number" @bind="_limitAmount" min="0" step="100" />
                </div>
                <VbButton Variant="primary" Size="sm" OnClick="SetLimit">Uložit limit</VbButton>
                @if (!string.IsNullOrEmpty(_limitMessage))
                {
                    <div class="form-success">@_limitMessage</div>
                }
            </ChildContent>
        </ContentCard>

        @* Pending Transactions *@
        <ContentCard Title="ČEKAJÍCÍ PLATBY">
            <ChildContent>
                @if (_pendingTransactions.Count == 0)
                {
                    <div style="color: var(--vb-text-muted);">Žádné čekající platby</div>
                }
                @foreach (var tx in _pendingTransactions)
                {
                    <div class="pending-tx-card">
                        <div class="pending-tx-info">
                            <div class="pending-tx-desc">@tx.Description</div>
                            <div class="pending-tx-amount">@tx.Amount.ToString("N2") @tx.Currency</div>
                            <div class="pending-tx-date">@tx.CreatedAt.ToString("dd.MM.yyyy HH:mm")</div>
                        </div>
                        <div class="pending-tx-actions">
                            <VbButton Variant="primary" Size="sm" OnClick="@(() => ApproveTransaction(tx.Id))">
                                Schválit
                            </VbButton>
                            <VbButton Variant="outline" Size="sm" OnClick="@(() => ShowRejectModal(tx.Id))">
                                Zamítnout
                            </VbButton>
                        </div>
                    </div>
                }
            </ChildContent>
        </ContentCard>

        @* Recent Transactions *@
        <ContentCard Title="POSLEDNÍ TRANSAKCE">
            <ChildContent>
                @if (_payments.Count == 0)
                {
                    <div style="color: var(--vb-text-muted);">Žádné transakce</div>
                }
                @foreach (var p in _payments.Take(10))
                {
                    <div class="transaction-row">
                        <span class="tx-desc">@(p.Description ?? "Platba")</span>
                        <span class="tx-amount @(p.SenderAccountId == _account?.Id ? "negative" : "positive")">
                            @(p.SenderAccountId == _account?.Id ? "-" : "+")@p.Amount.ToString("N2") @p.Currency
                        </span>
                    </div>
                }
            </ChildContent>
        </ContentCard>
    }

    @* Reject Modal *@
    @if (_showRejectModal)
    {
        <div class="modal-overlay" @onclick="() => _showRejectModal = false">
            <div class="modal-card" @onclick:stopPropagation="true">
                <h3>Důvod zamítnutí</h3>
                <div class="form-group">
                    <textarea @bind="_rejectReason" rows="3" placeholder="Vysvětlení pro dítě..."></textarea>
                </div>
                <div class="modal-actions">
                    <VbButton Variant="outline" OnClick="@(() => _showRejectModal = false)">Zrušit</VbButton>
                    <VbButton Variant="primary" OnClick="RejectTransaction">Zamítnout</VbButton>
                </div>
            </div>
        </div>
    }
</div>

@code {
    [Parameter] public Guid ChildId { get; set; }

    private UserResponse? _child;
    private AccountResponse? _account;
    private List<PendingTransactionDto> _pendingTransactions = [];
    private List<PaymentDto> _payments = [];
    private bool _loading = true;
    private decimal _limitAmount;
    private string? _limitMessage;
    private bool _showRejectModal;
    private Guid _rejectTxId;
    private string _rejectReason = "";
    private string _childName = "Dítě";

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _loading = true;
        try
        {
            _child = await Api.GetUserAsync(ChildId);
            _childName = _child is not null ? $"{_child.FirstName} {_child.LastName}" : "Dítě";

            var accounts = await Api.GetAccountsByOwnerAsync(ChildId);
            _account = accounts.FirstOrDefault();

            if (_account is not null)
            {
                _pendingTransactions = await Api.GetPendingTransactionsAsync(_account.Id);
                _payments = await Api.GetPaymentsByAccountAsync(_account.Id, 10);
            }
        }
        catch { /* handle errors */ }
        _loading = false;
    }

    private async Task SetLimit()
    {
        if (_account is null) return;
        try
        {
            // Call the set spending limit API (need to add to FairBankApiClient)
            await Api.SetSpendingLimitAsync(_account.Id, _limitAmount, "CZK");
            _limitMessage = "Limit nastaven!";
        }
        catch (Exception ex) { _limitMessage = ex.Message; }
    }

    private async Task ApproveTransaction(Guid txId)
    {
        await Api.ApproveTransactionAsync(txId, Auth.CurrentSession!.UserId);
        await LoadData();
    }

    private void ShowRejectModal(Guid txId)
    {
        _rejectTxId = txId;
        _rejectReason = "";
        _showRejectModal = true;
    }

    private async Task RejectTransaction()
    {
        await Api.RejectTransactionAsync(_rejectTxId, Auth.CurrentSession!.UserId, _rejectReason);
        _showRejectModal = false;
        await LoadData();
    }
}
```

**Step 4: Add missing API client method**

In `IFairBankApi.cs`, add:
```csharp
Task SetSpendingLimitAsync(Guid accountId, decimal limit, string currency);
```

In `FairBankApiClient.cs`, implement:
```csharp
public async Task SetSpendingLimitAsync(Guid accountId, decimal limit, string currency)
{
    await _http.PostAsJsonAsync($"api/v1/accounts/{accountId}/limits",
        new { Limit = limit, Currency = currency });
}
```

**Step 5: Commit**

```bash
git add src/FairBank.Web/Layout/SideNav.razor
git add src/FairBank.Web/Pages/Children.razor
git add src/FairBank.Web/Pages/ChildDetail.razor
git add src/FairBank.Web.Shared/Services/IFairBankApi.cs
git add src/FairBank.Web.Shared/Services/FairBankApiClient.cs
git commit -m "feat: add Children management pages with approval workflow and spending limits UI"
```

---

## Task 10: Adapt Overview page for Child role

**Files:**
- Modify: `src/FairBank.Web.Overview/Pages/Overview.razor`

**Step 1: Add child-specific sections**

In Overview.razor, add conditional content for Child role after the balance card:

```razor
@if (Auth.CurrentSession?.Role == "Child")
{
    @* Show spending limit info *@
    @if (_account?.SpendingLimit is not null)
    {
        <ContentCard Title="MŮJ LIMIT">
            <ChildContent>
                <div class="limit-info">
                    <span>Denní limit:</span>
                    <strong>@_account.SpendingLimit.Value.ToString("N0") @_account.Currency</strong>
                </div>
            </ChildContent>
        </ContentCard>
    }

    @* Show pending transactions status *@
    @if (_pendingCount > 0)
    {
        <ContentCard Title="ČEKAJÍCÍ PLATBY" CssClass="pending-notice">
            <ChildContent>
                <div style="color: var(--vb-gold);">
                    <strong>@_pendingCount</strong> platba čeká na schválení rodičem
                </div>
            </ChildContent>
        </ContentCard>
    }
}
```

This requires loading the account with spending limit info and checking pending transactions count in `OnInitializedAsync`:

```csharp
private AccountResponse? _account;
private int _pendingCount;

// In OnInitializedAsync, after loading accounts:
if (Auth.CurrentSession?.Role == "Child" && _account is not null)
{
    var pending = await Api.GetPendingTransactionsAsync(_account.Id);
    _pendingCount = pending.Count;
}
```

Note: `AccountResponse` needs to include the `SpendingLimit` field. This was added in Task 2 on the backend. The frontend `AccountResponse` model also needs updating.

**Step 2: Update frontend AccountResponse model**

In `src/FairBank.Web.Shared/Models/AccountResponse.cs`:

```csharp
public sealed record AccountResponse(
    Guid Id,
    Guid OwnerId,
    string AccountNumber,
    decimal Balance,
    string Currency,
    bool IsActive,
    DateTime CreatedAt,
    bool RequiresApproval = false,
    decimal? ApprovalThreshold = null,
    decimal? SpendingLimit = null);
```

**Step 3: Commit**

```bash
git add src/FairBank.Web.Overview/Pages/Overview.razor
git add src/FairBank.Web.Shared/Models/AccountResponse.cs
git commit -m "feat: adapt Overview page for Child role with limit info and pending status"
```

---

## Task 11: Add notification to approval/rejection flow

**Files:**
- Modify: `src/Services/Accounts/FairBank.Accounts.Application/Commands/ApproveTransaction/ApproveTransactionCommandHandler.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Application/Commands/RejectTransaction/RejectTransactionCommandHandler.cs`

The Accounts service needs an HTTP client for the notification endpoint (same pattern as Payments service).

**Step 1: Create notification port in Accounts**

Create `src/Services/Accounts/FairBank.Accounts.Application/Ports/INotificationClient.cs`:

```csharp
namespace FairBank.Accounts.Application.Ports;

public interface INotificationClient
{
    Task SendAsync(Guid userId, string type, string title, string message,
        Guid? relatedEntityId = null, string? relatedEntityType = null, CancellationToken ct = default);
}
```

Create `src/Services/Accounts/FairBank.Accounts.Infrastructure/HttpClients/NotificationHttpClient.cs`:

```csharp
using System.Net.Http.Json;
using FairBank.Accounts.Application.Ports;

namespace FairBank.Accounts.Infrastructure.HttpClients;

public sealed class NotificationHttpClient(HttpClient httpClient) : INotificationClient
{
    public async Task SendAsync(Guid userId, string type, string title, string message,
        Guid? relatedEntityId = null, string? relatedEntityType = null, CancellationToken ct = default)
    {
        try
        {
            await httpClient.PostAsJsonAsync("api/v1/notifications",
                new { UserId = userId, Type = type, Title = title, Message = message,
                      RelatedEntityId = relatedEntityId, RelatedEntityType = relatedEntityType }, ct);
        }
        catch { /* Fire and forget */ }
    }
}
```

Register in Accounts `DependencyInjection.cs`:

```csharp
// Add parameter: string identityApiBaseUrl
services.AddHttpClient<INotificationClient, NotificationHttpClient>(client =>
{
    client.BaseAddress = new Uri(identityApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
```

Update `Program.cs` to pass the Identity API URL.

Update `docker-compose.yml` to add `Services__IdentityApi: http://identity-api:8080` to `accounts-api`.

**Step 2: Inject into ApproveTransactionCommandHandler**

Add `INotificationClient notificationClient` to constructor. After approval + withdrawal, send notification to the child:

```csharp
// Notify child about approval
await notificationClient.SendAsync(
    tx.RequestedBy,
    "TransactionApproved",
    "Platba schválena",
    $"Tvá platba {tx.Amount.Amount} {tx.Amount.Currency} byla schválena.",
    tx.Id, "PendingTransaction", ct);
```

**Step 3: Inject into RejectTransactionCommandHandler**

Add `INotificationClient notificationClient` to constructor. After rejection, send notification:

```csharp
// Notify child about rejection
await notificationClient.SendAsync(
    tx.RequestedBy,
    "TransactionRejected",
    "Platba zamítnuta",
    $"Tvá platba {tx.Amount.Amount} {tx.Amount.Currency} byla zamítnuta: {request.Reason}",
    tx.Id, "PendingTransaction", ct);
```

**Step 4: Update existing tests**

Add `INotificationClient` mock to `ApproveTransactionCommandHandlerTests` and `RejectTransactionCommandHandlerTests`.

**Step 5: Run tests**

```bash
dotnet test tests/FairBank.Accounts.UnitTests/ --verbosity normal
```

Expected: All pass.

**Step 6: Commit**

```bash
git add src/Services/Accounts/FairBank.Accounts.Application/Ports/INotificationClient.cs
git add src/Services/Accounts/FairBank.Accounts.Infrastructure/HttpClients/NotificationHttpClient.cs
git add src/Services/Accounts/FairBank.Accounts.Infrastructure/DependencyInjection.cs
git add src/Services/Accounts/FairBank.Accounts.Api/Program.cs
git add src/Services/Accounts/FairBank.Accounts.Application/Commands/ApproveTransaction/ApproveTransactionCommandHandler.cs
git add src/Services/Accounts/FairBank.Accounts.Application/Commands/RejectTransaction/RejectTransactionCommandHandler.cs
git add docker-compose.yml
git add tests/FairBank.Accounts.UnitTests/
git commit -m "feat: add notification to approval/rejection workflow"
```

---

## Task 12: Final integration — add UserResponse ParentId to frontend and ensure family chat auto-creation

**Files:**
- Modify: `src/FairBank.Web.Shared/Models/UserResponse.cs` (add ParentId)
- Modify: `src/FairBank.Web/Pages/Children.razor` (auto-create family chat)

**Step 1: Update UserResponse**

```csharp
public sealed record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    Guid? ParentId = null);
```

Also update Identity service `UserResponse` DTO to include `ParentId`:

In `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/UserResponse.cs` (or wherever it lives), add `ParentId`.

**Step 2: Auto-create family chat on child creation**

In `Children.razor`, in the `CreateChild` method, after creating the account:

```csharp
// Auto-create family chat conversation
try
{
    await Api.GetOrCreateFamilyChatAsync(Auth.CurrentSession!.UserId, child.Id, child.FirstName);
}
catch { /* Chat creation failure is non-critical */ }
```

Add to `IFairBankApi`:
```csharp
Task GetOrCreateFamilyChatAsync(Guid parentId, Guid childId, string childLabel);
```

Implement in `FairBankApiClient`:
```csharp
public async Task GetOrCreateFamilyChatAsync(Guid parentId, Guid childId, string childLabel)
{
    await _http.PostAsync(
        $"api/v1/chat/conversations/family?parentId={parentId}&childId={childId}&childLabel={Uri.EscapeDataString(childLabel)}", null);
}
```

**Step 3: Run all tests**

```bash
dotnet test --verbosity normal
```

Expected: All pass.

**Step 4: Commit**

```bash
git add src/FairBank.Web.Shared/Models/UserResponse.cs
git add src/FairBank.Web/Pages/Children.razor
git add src/FairBank.Web.Shared/Services/IFairBankApi.cs
git add src/FairBank.Web.Shared/Services/FairBankApiClient.cs
git commit -m "feat: auto-provision account and family chat when creating child, add ParentId to UserResponse"
```

---

## Task 13: Docker build verification

**Step 1: Build all services**

```bash
cd /home/kamil/Job/fai/CSAD
docker compose build
```

Expected: All images build successfully.

**Step 2: Start stack and verify health**

```bash
docker compose up -d
# Wait for services
sleep 15
# Check health endpoints
curl http://localhost:8080/identity/health
curl http://localhost:8080/accounts/health
curl http://localhost:8080/payments/health
```

**Step 3: Test notification endpoint**

```bash
curl -X POST http://localhost:8080/api/v1/notifications \
  -H "Content-Type: application/json" \
  -d '{"userId":"00000000-0000-0000-0000-000000000001","type":"TransactionPending","title":"Test","message":"Test notification"}'
```

Expected: 201 Created

**Step 4: Commit any fixes**

```bash
git add -A
git commit -m "fix: docker build and integration verification"
```
