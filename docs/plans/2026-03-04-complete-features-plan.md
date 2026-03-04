# Complete Features Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement all 7 missing feature areas (cards, savings backend, investments backend, profile editing, admin management, child account UI, banker dashboard) to reach 100% coverage of the banking app specification.

**Architecture:** Extend the Accounts service (Marten event sourcing) with Card, SavingsGoal, SavingsRule, and Investment aggregates. Extend the Identity service (EF Core) with admin management and profile editing commands. Add one Chat service endpoint for banker clients. Build/update 7 frontend pages.

**Tech Stack:** .NET 10, Marten 8.22.1, EF Core 10, MediatR 14, FluentValidation 12, Blazor WASM, xUnit + FluentAssertions + NSubstitute

---

## Task 1: Card Domain — Aggregate, Events, Enums

**Files:**
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Enums/CardType.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/CardIssued.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/CardFrozen.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/CardUnfrozen.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/CardLimitSet.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/CardSettingsChanged.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/CardDeactivated.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/Card.cs`
- Test: `tests/FairBank.Accounts.UnitTests/Domain/CardTests.cs`

**Step 1: Create CardType enum**

```csharp
// src/Services/Accounts/FairBank.Accounts.Domain/Enums/CardType.cs
namespace FairBank.Accounts.Domain.Enums;

public enum CardType
{
    Debit = 0,
    Credit = 1
}
```

**Step 2: Create all 6 domain events**

```csharp
// src/Services/Accounts/FairBank.Accounts.Domain/Events/CardIssued.cs
using FairBank.Accounts.Domain.Enums;
namespace FairBank.Accounts.Domain.Events;

public sealed record CardIssued(
    Guid CardId,
    Guid AccountId,
    string CardNumber,
    string HolderName,
    DateTime ExpirationDate,
    CardType Type,
    DateTime OccurredAt);
```

```csharp
// CardFrozen.cs
namespace FairBank.Accounts.Domain.Events;
public sealed record CardFrozen(Guid CardId, DateTime OccurredAt);
```

```csharp
// CardUnfrozen.cs
namespace FairBank.Accounts.Domain.Events;
public sealed record CardUnfrozen(Guid CardId, DateTime OccurredAt);
```

```csharp
// CardLimitSet.cs
using FairBank.Accounts.Domain.Enums;
namespace FairBank.Accounts.Domain.Events;
public sealed record CardLimitSet(
    Guid CardId,
    decimal? DailyLimit,
    decimal? MonthlyLimit,
    Currency Currency,
    DateTime OccurredAt);
```

```csharp
// CardSettingsChanged.cs
namespace FairBank.Accounts.Domain.Events;
public sealed record CardSettingsChanged(
    Guid CardId,
    bool OnlinePaymentsEnabled,
    bool ContactlessEnabled,
    DateTime OccurredAt);
```

```csharp
// CardDeactivated.cs
namespace FairBank.Accounts.Domain.Events;
public sealed record CardDeactivated(Guid CardId, DateTime OccurredAt);
```

**Step 3: Create Card aggregate**

Follow the exact same pattern as `Account.cs`: sealed class, `[JsonInclude]` properties with private setters, `[JsonConstructor]` private constructor, `_uncommittedEvents` list, static `Create` factory, Apply methods per event.

```csharp
// src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/Card.cs
using System.Text.Json.Serialization;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.Domain.Aggregates;

public sealed class Card
{
    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public Guid AccountId { get; private set; }
    [JsonInclude] public string CardNumber { get; private set; } = null!;
    [JsonInclude] public string HolderName { get; private set; } = null!;
    [JsonInclude] public DateTime ExpirationDate { get; private set; }
    [JsonInclude] public string CVV { get; private set; } = null!;
    [JsonInclude] public CardType Type { get; private set; }
    [JsonInclude] public bool IsActive { get; private set; }
    [JsonInclude] public bool IsFrozen { get; private set; }
    [JsonInclude] public Money? DailyLimit { get; private set; }
    [JsonInclude] public Money? MonthlyLimit { get; private set; }
    [JsonInclude] public bool OnlinePaymentsEnabled { get; private set; }
    [JsonInclude] public bool ContactlessEnabled { get; private set; }
    [JsonInclude] public DateTime CreatedAt { get; private set; }

    private readonly List<object> _uncommittedEvents = [];

    [JsonConstructor]
    private Card() { }

    public static Card Create(Guid accountId, string holderName, CardType type, Currency currency)
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            CardNumber = GenerateCardNumber(),
            HolderName = holderName,
            ExpirationDate = DateTime.UtcNow.AddYears(4),
            CVV = GenerateCVV(),
            Type = type,
            IsActive = true,
            IsFrozen = false,
            OnlinePaymentsEnabled = true,
            ContactlessEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        card.RaiseEvent(new CardIssued(
            card.Id, accountId, card.CardNumber, holderName,
            card.ExpirationDate, type, DateTime.UtcNow));

        return card;
    }

    public void Freeze()
    {
        if (!IsActive) throw new InvalidOperationException("Card is not active.");
        if (IsFrozen) throw new InvalidOperationException("Card is already frozen.");
        IsFrozen = true;
        RaiseEvent(new CardFrozen(Id, DateTime.UtcNow));
    }

    public void Unfreeze()
    {
        if (!IsActive) throw new InvalidOperationException("Card is not active.");
        if (!IsFrozen) throw new InvalidOperationException("Card is not frozen.");
        IsFrozen = false;
        RaiseEvent(new CardUnfrozen(Id, DateTime.UtcNow));
    }

    public void SetLimits(Money? dailyLimit, Money? monthlyLimit)
    {
        if (!IsActive) throw new InvalidOperationException("Card is not active.");
        DailyLimit = dailyLimit;
        MonthlyLimit = monthlyLimit;
        var currency = dailyLimit?.Currency ?? monthlyLimit?.Currency ?? Enums.Currency.CZK;
        RaiseEvent(new CardLimitSet(Id, dailyLimit?.Amount, monthlyLimit?.Amount, currency, DateTime.UtcNow));
    }

    public void UpdateSettings(bool onlinePayments, bool contactless)
    {
        if (!IsActive) throw new InvalidOperationException("Card is not active.");
        OnlinePaymentsEnabled = onlinePayments;
        ContactlessEnabled = contactless;
        RaiseEvent(new CardSettingsChanged(Id, onlinePayments, contactless, DateTime.UtcNow));
    }

    public void Deactivate()
    {
        if (!IsActive) throw new InvalidOperationException("Card is already deactivated.");
        IsActive = false;
        RaiseEvent(new CardDeactivated(Id, DateTime.UtcNow));
    }

    // --- Marten Apply methods ---
    public void Apply(CardIssued e)
    {
        Id = e.CardId; AccountId = e.AccountId; CardNumber = e.CardNumber;
        HolderName = e.HolderName; ExpirationDate = e.ExpirationDate;
        Type = e.Type; IsActive = true; IsFrozen = false;
        OnlinePaymentsEnabled = true; ContactlessEnabled = true;
        CreatedAt = e.OccurredAt;
    }
    public void Apply(CardFrozen _) => IsFrozen = true;
    public void Apply(CardUnfrozen _) => IsFrozen = false;
    public void Apply(CardLimitSet e)
    {
        DailyLimit = e.DailyLimit.HasValue ? Money.Create(e.DailyLimit.Value, e.Currency) : null;
        MonthlyLimit = e.MonthlyLimit.HasValue ? Money.Create(e.MonthlyLimit.Value, e.Currency) : null;
    }
    public void Apply(CardSettingsChanged e)
    {
        OnlinePaymentsEnabled = e.OnlinePaymentsEnabled;
        ContactlessEnabled = e.ContactlessEnabled;
    }
    public void Apply(CardDeactivated _) => IsActive = false;

    public IReadOnlyList<object> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();
    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();
    private void RaiseEvent(object @event) => _uncommittedEvents.Add(@event);

    /// <summary>Masked display: **** **** **** 1234</summary>
    public string MaskedNumber => $"**** **** **** {CardNumber[^4..]}";

    private static string GenerateCardNumber()
    {
        var rng = Random.Shared;
        return $"4{rng.Next(100, 999):D3} {rng.Next(1000, 9999):D4} {rng.Next(1000, 9999):D4} {rng.Next(1000, 9999):D4}";
    }

    private static string GenerateCVV() => Random.Shared.Next(100, 999).ToString();
}
```

**Step 4: Write domain unit tests**

```csharp
// tests/FairBank.Accounts.UnitTests/Domain/CardTests.cs
using FluentAssertions;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;
using FairBank.Accounts.Domain.ValueObjects;

namespace FairBank.Accounts.UnitTests.Domain;

public class CardTests
{
    [Fact]
    public void Create_ShouldInitializeActiveCard()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novák", CardType.Debit, Currency.CZK);

        card.Id.Should().NotBe(Guid.Empty);
        card.IsActive.Should().BeTrue();
        card.IsFrozen.Should().BeFalse();
        card.OnlinePaymentsEnabled.Should().BeTrue();
        card.ContactlessEnabled.Should().BeTrue();
        card.CardNumber.Should().NotBeNullOrEmpty();
        card.CVV.Should().HaveLength(3);
        card.ExpirationDate.Should().BeAfter(DateTime.UtcNow.AddYears(3));
    }

    [Fact]
    public void Create_ShouldRaiseCardIssuedEvent()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novák", CardType.Debit, Currency.CZK);
        var events = card.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<CardIssued>();
    }

    [Fact]
    public void Freeze_ShouldSetFrozenTrue()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novák", CardType.Debit, Currency.CZK);
        card.Freeze();
        card.IsFrozen.Should().BeTrue();
    }

    [Fact]
    public void Freeze_WhenAlreadyFrozen_ShouldThrow()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novák", CardType.Debit, Currency.CZK);
        card.Freeze();
        var act = () => card.Freeze();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Unfreeze_ShouldSetFrozenFalse()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novák", CardType.Debit, Currency.CZK);
        card.Freeze();
        card.Unfreeze();
        card.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public void SetLimits_ShouldUpdateLimits()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novák", CardType.Debit, Currency.CZK);
        card.SetLimits(Money.Create(5000, Currency.CZK), Money.Create(50000, Currency.CZK));
        card.DailyLimit!.Amount.Should().Be(5000);
        card.MonthlyLimit!.Amount.Should().Be(50000);
    }

    [Fact]
    public void Deactivate_ShouldSetInactive()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novák", CardType.Debit, Currency.CZK);
        card.Deactivate();
        card.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novák", CardType.Debit, Currency.CZK);
        card.Deactivate();
        var act = () => card.Deactivate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateSettings_ShouldChangeFlags()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novák", CardType.Debit, Currency.CZK);
        card.UpdateSettings(false, false);
        card.OnlinePaymentsEnabled.Should().BeFalse();
        card.ContactlessEnabled.Should().BeFalse();
    }

    [Fact]
    public void MaskedNumber_ShouldShowLastFourDigits()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novák", CardType.Debit, Currency.CZK);
        card.MaskedNumber.Should().StartWith("**** **** **** ");
        card.MaskedNumber.Should().HaveLength(19);
    }
}
```

**Step 5: Run tests, verify they pass**

```bash
dotnet test tests/FairBank.Accounts.UnitTests --filter "CardTests" -v minimal
```

**Step 6: Commit**

```bash
git add src/Services/Accounts/FairBank.Accounts.Domain/ tests/FairBank.Accounts.UnitTests/Domain/CardTests.cs
git commit -m "feat(accounts): add Card aggregate with events and domain tests"
```

---

## Task 2: Card Application — Commands, Queries, DTOs, Port

**Files:**
- Create: `src/Services/Accounts/FairBank.Accounts.Application/DTOs/CardResponse.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Ports/ICardEventStore.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/IssueCard/IssueCardCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/IssueCard/IssueCardCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/FreezeCard/FreezeCardCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/FreezeCard/FreezeCardCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/UnfreezeCard/UnfreezeCardCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/UnfreezeCard/UnfreezeCardCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/SetCardLimits/SetCardLimitsCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/SetCardLimits/SetCardLimitsCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/UpdateCardSettings/UpdateCardSettingsCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/UpdateCardSettings/UpdateCardSettingsCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/DeactivateCard/DeactivateCardCommand.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Commands/DeactivateCard/DeactivateCardCommandHandler.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetCardsByAccount/GetCardsByAccountQuery.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Queries/GetCardsByAccount/GetCardsByAccountQueryHandler.cs`
- Test: `tests/FairBank.Accounts.UnitTests/Application/IssueCardCommandHandlerTests.cs`

**Step 1: Create CardResponse DTO**

```csharp
// src/Services/Accounts/FairBank.Accounts.Application/DTOs/CardResponse.cs
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Application.DTOs;

public sealed record CardResponse(
    Guid Id,
    Guid AccountId,
    string MaskedNumber,
    string HolderName,
    DateTime ExpirationDate,
    CardType Type,
    bool IsActive,
    bool IsFrozen,
    decimal? DailyLimit,
    decimal? MonthlyLimit,
    bool OnlinePaymentsEnabled,
    bool ContactlessEnabled,
    DateTime CreatedAt);
```

**Step 2: Create ICardEventStore port**

```csharp
// src/Services/Accounts/FairBank.Accounts.Application/Ports/ICardEventStore.cs
using FairBank.Accounts.Domain.Aggregates;

namespace FairBank.Accounts.Application.Ports;

public interface ICardEventStore
{
    Task<Card?> LoadAsync(Guid cardId, CancellationToken ct = default);
    Task<IReadOnlyList<Card>> LoadByAccountAsync(Guid accountId, CancellationToken ct = default);
    Task StartStreamAsync(Card card, CancellationToken ct = default);
    Task AppendEventsAsync(Card card, CancellationToken ct = default);
}
```

**Step 3: Create all commands and handlers**

Follow exact pattern of `CreateAccountCommand`/`Handler` and `DepositMoneyCommand`/`Handler`:

```csharp
// IssueCardCommand.cs
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Domain.Enums;
using MediatR;
namespace FairBank.Accounts.Application.Commands.IssueCard;
public sealed record IssueCardCommand(
    Guid AccountId,
    string HolderName,
    CardType Type = CardType.Debit) : IRequest<CardResponse>;
```

```csharp
// IssueCardCommandHandler.cs
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using MediatR;
namespace FairBank.Accounts.Application.Commands.IssueCard;
public sealed class IssueCardCommandHandler(
    ICardEventStore cardStore,
    IAccountEventStore accountStore) : IRequestHandler<IssueCardCommand, CardResponse>
{
    public async Task<CardResponse> Handle(IssueCardCommand request, CancellationToken ct)
    {
        var account = await accountStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException($"Account {request.AccountId} not found.");
        var card = Card.Create(request.AccountId, request.HolderName, request.Type, account.Balance.Currency);
        await cardStore.StartStreamAsync(card, ct);
        return new CardResponse(card.Id, card.AccountId, card.MaskedNumber, card.HolderName,
            card.ExpirationDate, card.Type, card.IsActive, card.IsFrozen,
            card.DailyLimit?.Amount, card.MonthlyLimit?.Amount,
            card.OnlinePaymentsEnabled, card.ContactlessEnabled, card.CreatedAt);
    }
}
```

```csharp
// FreezeCardCommand.cs
using MediatR;
namespace FairBank.Accounts.Application.Commands.FreezeCard;
public sealed record FreezeCardCommand(Guid CardId) : IRequest;
```

```csharp
// FreezeCardCommandHandler.cs
using FairBank.Accounts.Application.Ports;
using MediatR;
namespace FairBank.Accounts.Application.Commands.FreezeCard;
public sealed class FreezeCardCommandHandler(ICardEventStore cardStore) : IRequestHandler<FreezeCardCommand>
{
    public async Task Handle(FreezeCardCommand request, CancellationToken ct)
    {
        var card = await cardStore.LoadAsync(request.CardId, ct)
            ?? throw new InvalidOperationException($"Card {request.CardId} not found.");
        card.Freeze();
        await cardStore.AppendEventsAsync(card, ct);
    }
}
```

Same pattern for `UnfreezeCard`, `SetCardLimits(CardId, DailyLimit?, MonthlyLimit?, Currency)`, `UpdateCardSettings(CardId, OnlinePayments, Contactless)`, `DeactivateCard(CardId)`.

```csharp
// GetCardsByAccountQuery.cs
using FairBank.Accounts.Application.DTOs;
using MediatR;
namespace FairBank.Accounts.Application.Queries.GetCardsByAccount;
public sealed record GetCardsByAccountQuery(Guid AccountId) : IRequest<IReadOnlyList<CardResponse>>;
```

```csharp
// GetCardsByAccountQueryHandler.cs
using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;
namespace FairBank.Accounts.Application.Queries.GetCardsByAccount;
public sealed class GetCardsByAccountQueryHandler(ICardEventStore cardStore)
    : IRequestHandler<GetCardsByAccountQuery, IReadOnlyList<CardResponse>>
{
    public async Task<IReadOnlyList<CardResponse>> Handle(GetCardsByAccountQuery request, CancellationToken ct)
    {
        var cards = await cardStore.LoadByAccountAsync(request.AccountId, ct);
        return cards.Select(c => new CardResponse(c.Id, c.AccountId, c.MaskedNumber, c.HolderName,
            c.ExpirationDate, c.Type, c.IsActive, c.IsFrozen,
            c.DailyLimit?.Amount, c.MonthlyLimit?.Amount,
            c.OnlinePaymentsEnabled, c.ContactlessEnabled, c.CreatedAt)).ToList();
    }
}
```

**Step 4: Write handler tests**

```csharp
// tests/FairBank.Accounts.UnitTests/Application/IssueCardCommandHandlerTests.cs
using FluentAssertions;
using NSubstitute;
using FairBank.Accounts.Application.Commands.IssueCard;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.UnitTests.Application;

public class IssueCardCommandHandlerTests
{
    private readonly ICardEventStore _cardStore = Substitute.For<ICardEventStore>();
    private readonly IAccountEventStore _accountStore = Substitute.For<IAccountEventStore>();

    [Fact]
    public async Task Handle_WithValidCommand_ShouldIssueCard()
    {
        var account = Account.Create(Guid.NewGuid(), Currency.CZK);
        _accountStore.LoadAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var handler = new IssueCardCommandHandler(_cardStore, _accountStore);
        var command = new IssueCardCommand(account.Id, "Jan Novák", CardType.Debit);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.AccountId.Should().Be(account.Id);
        result.HolderName.Should().Be("Jan Novák");
        result.IsActive.Should().BeTrue();
        result.IsFrozen.Should().BeFalse();
        result.Type.Should().Be(CardType.Debit);
        await _cardStore.Received(1).StartStreamAsync(Arg.Any<Card>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentAccount_ShouldThrow()
    {
        _accountStore.LoadAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);
        var handler = new IssueCardCommandHandler(_cardStore, _accountStore);
        var act = () => handler.Handle(new IssueCardCommand(Guid.NewGuid(), "Jan", CardType.Debit), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
```

**Step 5: Run tests, commit**

```bash
dotnet test tests/FairBank.Accounts.UnitTests --filter "IssueCardCommandHandlerTests" -v minimal
git add src/Services/Accounts/FairBank.Accounts.Application/ tests/FairBank.Accounts.UnitTests/Application/IssueCardCommandHandlerTests.cs
git commit -m "feat(accounts): add Card commands, queries, DTOs and handler tests"
```

---

## Task 3: Card Infrastructure + API Endpoints

**Files:**
- Create: `src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/MartenCardEventStore.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Api/Endpoints/CardEndpoints.cs`
- Modify: `src/Services/Accounts/FairBank.Accounts.Infrastructure/DependencyInjection.cs` — register Card snapshot + store
- Modify: `src/Services/Accounts/FairBank.Accounts.Api/Program.cs` — map card endpoints

**Step 1: Create MartenCardEventStore**

Follow exact pattern of `MartenAccountEventStore.cs`:

```csharp
// src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/MartenCardEventStore.cs
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using Marten;

namespace FairBank.Accounts.Infrastructure.Persistence;

public sealed class MartenCardEventStore(IDocumentSession session) : ICardEventStore
{
    public async Task<Card?> LoadAsync(Guid cardId, CancellationToken ct = default)
        => await session.Events.AggregateStreamAsync<Card>(cardId, token: ct);

    public async Task<IReadOnlyList<Card>> LoadByAccountAsync(Guid accountId, CancellationToken ct = default)
        => await session.Query<Card>().Where(c => c.AccountId == accountId && c.IsActive).ToListAsync(ct);

    public async Task StartStreamAsync(Card card, CancellationToken ct = default)
    {
        var events = card.GetUncommittedEvents();
        if (events.Count == 0) return;
        session.Events.StartStream<Card>(card.Id, events.ToArray());
        card.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }

    public async Task AppendEventsAsync(Card card, CancellationToken ct = default)
    {
        var events = card.GetUncommittedEvents();
        if (events.Count == 0) return;
        session.Events.Append(card.Id, events.ToArray());
        card.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }
}
```

**Step 2: Register in DI**

Add to `DependencyInjection.cs` in Infrastructure:
- `options.Projections.Snapshot<Card>(SnapshotLifecycle.Inline);`
- `services.AddScoped<ICardEventStore, MartenCardEventStore>();`

**Step 3: Create CardEndpoints**

```csharp
// src/Services/Accounts/FairBank.Accounts.Api/Endpoints/CardEndpoints.cs
using FairBank.Accounts.Application.Commands.DeactivateCard;
using FairBank.Accounts.Application.Commands.FreezeCard;
using FairBank.Accounts.Application.Commands.IssueCard;
using FairBank.Accounts.Application.Commands.SetCardLimits;
using FairBank.Accounts.Application.Commands.UnfreezeCard;
using FairBank.Accounts.Application.Commands.UpdateCardSettings;
using FairBank.Accounts.Application.Queries.GetCardsByAccount;
using MediatR;

namespace FairBank.Accounts.Api.Endpoints;

public static class CardEndpoints
{
    public static RouteGroupBuilder MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Cards");

        group.MapPost("/accounts/{accountId:guid}/cards", async (Guid accountId, IssueCardCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { AccountId = accountId });
            return Results.Created($"/api/v1/cards/{result.Id}", result);
        });

        group.MapGet("/accounts/{accountId:guid}/cards", async (Guid accountId, ISender sender) =>
        {
            var result = await sender.Send(new GetCardsByAccountQuery(accountId));
            return Results.Ok(result);
        });

        group.MapPost("/cards/{id:guid}/freeze", async (Guid id, ISender sender) =>
        {
            await sender.Send(new FreezeCardCommand(id));
            return Results.NoContent();
        });

        group.MapPost("/cards/{id:guid}/unfreeze", async (Guid id, ISender sender) =>
        {
            await sender.Send(new UnfreezeCardCommand(id));
            return Results.NoContent();
        });

        group.MapPut("/cards/{id:guid}/limits", async (Guid id, SetCardLimitsCommand command, ISender sender) =>
        {
            await sender.Send(command with { CardId = id });
            return Results.NoContent();
        });

        group.MapPut("/cards/{id:guid}/settings", async (Guid id, UpdateCardSettingsCommand command, ISender sender) =>
        {
            await sender.Send(command with { CardId = id });
            return Results.NoContent();
        });

        group.MapDelete("/cards/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeactivateCardCommand(id));
            return Results.NoContent();
        });

        return group;
    }
}
```

**Step 4: Wire up in Program.cs**

Add `app.MapCardEndpoints();` after `app.MapAccountEndpoints();`.

**Step 5: Commit**

```bash
git commit -m "feat(accounts): add Card infrastructure, API endpoints and DI registration"
```

---

## Task 4: SavingsGoal Domain + Application + Infrastructure + API

**Files:**
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/SavingsGoalCreated.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/SavingsDeposited.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/SavingsWithdrawn.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Events/SavingsGoalCompleted.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/SavingsGoal.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/DTOs/SavingsGoalResponse.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Application/Ports/ISavingsGoalEventStore.cs`
- Create: Commands: `CreateSavingsGoal`, `DepositToSavingsGoal`, `WithdrawFromSavingsGoal`, `DeleteSavingsGoal`
- Create: Query: `GetSavingsGoalsByAccount`
- Create: `src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/MartenSavingsGoalEventStore.cs`
- Create: `src/Services/Accounts/FairBank.Accounts.Api/Endpoints/SavingsGoalEndpoints.cs`
- Test: `tests/FairBank.Accounts.UnitTests/Domain/SavingsGoalTests.cs`

**Same patterns as Card (Task 1-3). Key differences:**

**SavingsGoal aggregate:**
```csharp
public sealed class SavingsGoal
{
    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public Guid AccountId { get; private set; }
    [JsonInclude] public string Name { get; private set; } = null!;
    [JsonInclude] public string? Description { get; private set; }
    [JsonInclude] public Money TargetAmount { get; private set; } = null!;
    [JsonInclude] public Money CurrentAmount { get; private set; } = null!;
    [JsonInclude] public bool IsCompleted { get; private set; }
    [JsonInclude] public DateTime CreatedAt { get; private set; }
    [JsonInclude] public DateTime? CompletedAt { get; private set; }
    // ... _uncommittedEvents, Create, Deposit, Withdraw, Apply methods
}
```

**Methods:** `Create(accountId, name, description, targetAmount, currency)`, `Deposit(amount)` (auto-completes if target reached), `Withdraw(amount)`, Apply for each event.

**DTO:** `SavingsGoalResponse(Id, AccountId, Name, Description, TargetAmount, CurrentAmount, ProgressPercent, Currency, IsCompleted, CreatedAt)`

**Endpoints:**
- `POST /api/v1/accounts/{id}/savings-goals`
- `GET /api/v1/accounts/{id}/savings-goals`
- `POST /api/v1/savings-goals/{id}/deposit`
- `POST /api/v1/savings-goals/{id}/withdraw`
- `DELETE /api/v1/savings-goals/{id}`

**Tests:** Create, Deposit increases amount, Deposit auto-completes, Withdraw decreases, Withdraw below zero throws.

Register snapshot and store in Infrastructure DI. Wire endpoints in Program.cs.

```bash
git commit -m "feat(accounts): add SavingsGoal aggregate with full CQRS stack and tests"
```

---

## Task 5: SavingsRule Domain + Application + Infrastructure + API

**Same patterns. SavingsRule aggregate:**

```csharp
// Enum
namespace FairBank.Accounts.Domain.Enums;
public enum SavingsRuleType { RoundUp = 0, FixedWeekly = 1, FixedMonthly = 2, PercentageOfIncome = 3 }
```

```csharp
public sealed class SavingsRule
{
    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public Guid AccountId { get; private set; }
    [JsonInclude] public string Name { get; private set; } = null!;
    [JsonInclude] public string? Description { get; private set; }
    [JsonInclude] public SavingsRuleType Type { get; private set; }
    [JsonInclude] public decimal Amount { get; private set; }
    [JsonInclude] public bool IsEnabled { get; private set; }
    [JsonInclude] public DateTime CreatedAt { get; private set; }
}
```

**Events:** `SavingsRuleCreated`, `SavingsRuleToggled`

**Commands:** `CreateSavingsRule`, `ToggleSavingsRule`
**Query:** `GetSavingsRulesByAccount`

**Endpoints:**
- `POST /api/v1/accounts/{id}/savings-rules`
- `GET /api/v1/accounts/{id}/savings-rules`
- `PUT /api/v1/savings-rules/{id}/toggle`

```bash
git commit -m "feat(accounts): add SavingsRule aggregate with CQRS stack and tests"
```

---

## Task 6: Investment Domain + Application + Infrastructure + API

**Same patterns. Investment aggregate:**

```csharp
// Enum
namespace FairBank.Accounts.Domain.Enums;
public enum InvestmentType { Stock = 0, Bond = 1, Crypto = 2, Fund = 3 }
```

```csharp
public sealed class Investment
{
    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public Guid AccountId { get; private set; }
    [JsonInclude] public string Name { get; private set; } = null!;
    [JsonInclude] public InvestmentType Type { get; private set; }
    [JsonInclude] public Money InvestedAmount { get; private set; } = null!;
    [JsonInclude] public Money CurrentValue { get; private set; } = null!;
    [JsonInclude] public decimal Units { get; private set; }
    [JsonInclude] public decimal PricePerUnit { get; private set; }
    [JsonInclude] public bool IsActive { get; private set; }
    [JsonInclude] public DateTime CreatedAt { get; private set; }
    [JsonInclude] public DateTime? SoldAt { get; private set; }
}
```

**Events:** `InvestmentCreated`, `InvestmentValueUpdated`, `InvestmentSold`

**Commands:** `CreateInvestment(AccountId, Name, Type, Amount, Units, PricePerUnit, Currency)`, `UpdateInvestmentValue(InvestmentId, NewPricePerUnit)`, `SellInvestment(InvestmentId)`

**Query:** `GetInvestmentsByAccount`, `GetInvestmentById`

**Endpoints:**
- `POST /api/v1/accounts/{id}/investments`
- `GET /api/v1/accounts/{id}/investments`
- `GET /api/v1/investments/{id}`
- `PUT /api/v1/investments/{id}/value`
- `POST /api/v1/investments/{id}/sell`

**Seeder:** Add demo investments to `AccountSeeder.cs` for the Client account (Akciový fond, Dluhopisový fond, Kryptoměny).

```bash
git commit -m "feat(accounts): add Investment aggregate with CQRS stack, seeder and tests"
```

---

## Task 7: Gateway Routes + Accounts Service Wiring

**Files:**
- Modify: `src/FairBank.ApiGateway/appsettings.json` — add routes for cards, savings-goals, savings-rules, investments
- Verify: All new endpoints mapped in `Program.cs`
- Verify: All new snapshots registered in Infrastructure `DependencyInjection.cs`

**New gateway routes to add:**

```json
"cards-route": {
  "ClusterId": "accounts-cluster",
  "Match": { "Path": "/api/v1/cards/{**catch-all}" }
},
"savings-goals-route": {
  "ClusterId": "accounts-cluster",
  "Match": { "Path": "/api/v1/savings-goals/{**catch-all}" }
},
"savings-rules-route": {
  "ClusterId": "accounts-cluster",
  "Match": { "Path": "/api/v1/savings-rules/{**catch-all}" }
},
"investments-route": {
  "ClusterId": "accounts-cluster",
  "Match": { "Path": "/api/v1/investments/{**catch-all}" }
}
```

Note: Account-level routes like `/api/v1/accounts/{id}/cards` already route to accounts-cluster via the existing accounts-route.

```bash
git commit -m "feat(gateway): add routes for cards, savings, investments endpoints"
```

---

## Task 8: Identity Service — Profile Editing

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ChangeEmail/ChangeEmailCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ChangeEmail/ChangeEmailCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ChangeEmail/ChangeEmailCommandValidator.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ChangePassword/ChangePasswordCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ChangePassword/ChangePasswordCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ChangePassword/ChangePasswordCommandValidator.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs` — add 2 new endpoints
- Test: `tests/FairBank.Identity.UnitTests/Application/ChangeEmailCommandHandlerTests.cs`
- Test: `tests/FairBank.Identity.UnitTests/Application/ChangePasswordCommandHandlerTests.cs`

**Step 1: Add domain method to User entity**

Add to `User.cs`:

```csharp
public void ChangeEmail(Email newEmail)
{
    Email = newEmail;
    UpdatedAt = DateTime.UtcNow;
}

public void ChangePassword(string newPasswordHash)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash, nameof(newPasswordHash));
    PasswordHash = newPasswordHash;
    UpdatedAt = DateTime.UtcNow;
}
```

**Step 2: Create ChangeEmailCommand + Handler**

```csharp
// ChangeEmailCommand.cs
using MediatR;
namespace FairBank.Identity.Application.Users.Commands.ChangeEmail;
public sealed record ChangeEmailCommand(Guid UserId, string NewEmail) : IRequest;
```

```csharp
// ChangeEmailCommandHandler.cs
using FairBank.Identity.Domain.Ports;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Application;
using MediatR;
namespace FairBank.Identity.Application.Users.Commands.ChangeEmail;
public sealed class ChangeEmailCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ChangeEmailCommand>
{
    public async Task Handle(ChangeEmailCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");
        var newEmail = Email.Create(request.NewEmail);
        if (await userRepository.ExistsWithEmailAsync(newEmail, ct))
            throw new InvalidOperationException("Email already taken.");
        user.ChangeEmail(newEmail);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
```

```csharp
// ChangeEmailCommandValidator.cs
using FluentValidation;
namespace FairBank.Identity.Application.Users.Commands.ChangeEmail;
public sealed class ChangeEmailCommandValidator : AbstractValidator<ChangeEmailCommand>
{
    public ChangeEmailCommandValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().EmailAddress();
    }
}
```

**Step 3: Create ChangePasswordCommand + Handler**

```csharp
// ChangePasswordCommand.cs
using MediatR;
namespace FairBank.Identity.Application.Users.Commands.ChangePassword;
public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest;
```

```csharp
// ChangePasswordCommandHandler.cs
using FairBank.Identity.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;
namespace FairBank.Identity.Application.Users.Commands.ChangePassword;
public sealed class ChangePasswordCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("User not found.");
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new InvalidOperationException("Current password is incorrect.");
        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.ChangePassword(newHash);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
```

Validator: same password rules as `RegisterUserCommandValidator`.

**Step 4: Add endpoints to UserEndpoints.cs**

```csharp
group.MapPut("/{id:guid}/email", async (Guid id, ChangeEmailCommand command, ISender sender) =>
{
    await sender.Send(command with { UserId = id });
    return Results.NoContent();
});

group.MapPut("/{id:guid}/password", async (Guid id, ChangePasswordCommand command, ISender sender) =>
{
    await sender.Send(command with { UserId = id });
    return Results.NoContent();
});
```

**Step 5: Write tests, run, commit**

```bash
git commit -m "feat(identity): add ChangeEmail and ChangePassword commands with validation and tests"
```

---

## Task 9: Identity Service — Admin User Management

**Files:**
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetAllUsers/GetAllUsersQuery.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetAllUsers/GetAllUsersQueryHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/PagedUsersResponse.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/UpdateUserRole/UpdateUserRoleCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/UpdateUserRole/UpdateUserRoleCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/DeactivateUser/DeactivateUserCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/DeactivateUser/DeactivateUserCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ActivateUser/ActivateUserCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/ActivateUser/ActivateUserCommandHandler.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/DeleteUser/DeleteUserCommand.cs`
- Create: `src/Services/Identity/FairBank.Identity.Application/Users/Commands/DeleteUser/DeleteUserCommandHandler.cs`
- Modify: `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs` — add 5 new endpoints
- Modify: `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs` — add ChangeRole, Activate methods
- Test: `tests/FairBank.Identity.UnitTests/Application/AdminCommandsTests.cs`

**Step 1: Add domain methods to User.cs**

```csharp
public void ChangeRole(UserRole newRole)
{
    Role = newRole;
    UpdatedAt = DateTime.UtcNow;
}

public void Activate()
{
    IsActive = true;
    UpdatedAt = DateTime.UtcNow;
}

public void Deactivate()
{
    IsActive = false;
    UpdatedAt = DateTime.UtcNow;
}
```

**Step 2: Create PagedUsersResponse DTO**

```csharp
namespace FairBank.Identity.Application.Users.DTOs;
public sealed record PagedUsersResponse(
    IReadOnlyList<UserResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
```

**Step 3: Create GetAllUsersQuery + Handler**

```csharp
// GetAllUsersQuery.cs
using FairBank.Identity.Application.Users.DTOs;
using FairBank.Identity.Domain.Enums;
using MediatR;
namespace FairBank.Identity.Application.Users.Queries.GetAllUsers;
public sealed record GetAllUsersQuery(
    int Page = 1,
    int PageSize = 20,
    UserRole? RoleFilter = null,
    string? SearchTerm = null) : IRequest<PagedUsersResponse>;
```

Handler: use `GetAllAsync()` from repository, apply filters in-memory (or extend repository with filtered query), paginate, return `PagedUsersResponse`.

**Step 4: Create admin command handlers** (UpdateUserRole, DeactivateUser, ActivateUser, DeleteUser)

Each follows same pattern: load user by ID, call domain method, save.

**Step 5: Add endpoints**

```csharp
group.MapGet("/", async ([AsParameters] GetAllUsersQuery query, ISender sender) =>
    Results.Ok(await sender.Send(query)));

group.MapPut("/{id:guid}/role", async (Guid id, UpdateUserRoleCommand command, ISender sender) =>
{
    await sender.Send(command with { UserId = id });
    return Results.NoContent();
});

group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender) =>
{
    await sender.Send(new DeactivateUserCommand(id));
    return Results.NoContent();
});

group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
{
    await sender.Send(new ActivateUserCommand(id));
    return Results.NoContent();
});

group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
{
    await sender.Send(new DeleteUserCommand(id));
    return Results.NoContent();
});
```

**Step 6: Tests, commit**

```bash
git commit -m "feat(identity): add admin user management commands and GetAllUsers query"
```

---

## Task 10: Chat Service — Banker Clients Endpoint

**Files:**
- Modify: `src/Services/Chat/FairBank.Chat.Api/Program.cs` — add GET endpoint

**Add endpoint** to return unique client IDs assigned to a banker:

```csharp
app.MapGet("/api/v1/chat/conversations/banker/{bankerId:guid}/clients", async (Guid bankerId, ChatDbContext db) =>
{
    var clients = await db.Conversations
        .Where(c => c.AssignedBankerId == bankerId && c.Status == ConversationStatus.Active)
        .Select(c => new { c.ClientId, c.ClientLabel, c.LastMessageAt })
        .Distinct()
        .ToListAsync();
    return Results.Ok(clients);
});
```

Also add to gateway if needed (should already route via `chat-route`).

```bash
git commit -m "feat(chat): add banker clients endpoint for dashboard"
```

---

## Task 11: Frontend — API Client + DTOs Update

**Files:**
- Create: `src/FairBank.Web.Shared/Models/CardDto.cs`
- Modify: `src/FairBank.Web.Shared/Models/SavingsGoalDto.cs` — add missing fields
- Modify: `src/FairBank.Web.Shared/Models/InvestmentDto.cs` — add missing fields
- Create: `src/FairBank.Web.Shared/Models/PagedUsersDto.cs`
- Modify: `src/FairBank.Web.Shared/Services/IFairBankApi.cs` — add new method signatures
- Modify: `src/FairBank.Web.Shared/Services/FairBankApiClient.cs` — add new method implementations

**New DTO:**

```csharp
// src/FairBank.Web.Shared/Models/CardDto.cs
namespace FairBank.Web.Shared.Models;
public sealed record CardDto(
    Guid Id, Guid AccountId, string MaskedNumber, string HolderName,
    DateTime ExpirationDate, string Type, bool IsActive, bool IsFrozen,
    decimal? DailyLimit, decimal? MonthlyLimit,
    bool OnlinePaymentsEnabled, bool ContactlessEnabled, DateTime CreatedAt);
```

**Update existing DTOs** to match backend responses (add AccountId, IsCompleted, etc.).

**New API client methods** (~20 new methods):

Cards: `GetCardsByAccountAsync`, `IssueCardAsync`, `FreezeCardAsync`, `UnfreezeCardAsync`, `SetCardLimitsAsync`, `UpdateCardSettingsAsync`, `DeactivateCardAsync`

Savings: `GetSavingsGoalsByAccountAsync`, `CreateSavingsGoalAsync`, `DepositToSavingsGoalAsync`, `WithdrawFromSavingsGoalAsync`, `DeleteSavingsGoalAsync`, `GetSavingsRulesByAccountAsync`, `CreateSavingsRuleAsync`, `ToggleSavingsRuleAsync`

Investments: `GetInvestmentsByAccountAsync`, `CreateInvestmentAsync`, `SellInvestmentAsync`

Admin: `GetAllUsersAsync(page, pageSize, role?, search?)`, `UpdateUserRoleAsync`, `DeactivateUserAsync`, `ActivateUserAsync`, `DeleteUserAsync`

Profile: `ChangeEmailAsync`, `ChangePasswordAsync`

```bash
git commit -m "feat(frontend): add DTOs and API client methods for all new features"
```

---

## Task 12: Frontend — Cards Page

**Files:**
- Create: `src/FairBank.Web.Cards/FairBank.Web.Cards.csproj`
- Create: `src/FairBank.Web.Cards/Pages/Cards.razor`
- Create: `src/FairBank.Web.Cards/_Imports.razor`
- Modify: `src/FairBank.Web/FairBank.Web.csproj` — add project reference
- Modify: `FairBank.slnx` — add project to solution
- Modify: `src/FairBank.Web/Layout/SideNav.razor` — add "Karty" link
- Modify: `src/FairBank.Web/Layout/BottomNav.razor` — add "Karty" link

**Cards.razor page** (`@page "/karty"`):
- Load cards via `GetCardsByAccountAsync`
- Card list with masked number, type badge, status (Active/Frozen)
- Detail view: freeze/unfreeze toggle, online/contactless toggles, limit inputs
- "Vydat novou kartu" button → form with holder name + type dropdown
- "Zrušit kartu" button with confirmation
- VbIcon: `credit-card` (add to VbIcon.razor if missing)

**Navigation:** Add between "Platby" and "Spoření":
```html
<NavLink class="side-nav-item" href="karty">
    <span class="side-nav-icon"><VbIcon Name="credit-card" Size="sm" /></span>
    <span class="side-nav-label">Karty</span>
</NavLink>
```

```bash
git commit -m "feat(frontend): add Cards page with issue, freeze, limits and settings"
```

---

## Task 13: Frontend — Savings Page Update

**Files:**
- Modify: `src/FairBank.Web.Savings/Pages/Savings.razor` — replace demo data with real API

**Changes:**
- `OnInitializedAsync`: load account, then `GetSavingsGoalsByAccountAsync` and `GetSavingsRulesByAccountAsync`
- Replace hardcoded goals with real data
- Add "Nový cíl" button → CreateSavingsGoal form (name, description, target amount)
- Add "Vložit" / "Vybrat" buttons per goal
- Replace hardcoded rules with real data + working toggle switches via `ToggleSavingsRuleAsync`
- Add "Nové pravidlo" form

```bash
git commit -m "feat(frontend): connect Savings page to real API endpoints"
```

---

## Task 14: Frontend — Investments Page Update

**Files:**
- Modify: `src/FairBank.Web.Investments/Pages/Investments.razor` — replace demo data with real API

**Changes:**
- Load investments via `GetInvestmentsByAccountAsync`
- Portfolio overview: sum of all CurrentValue
- Asset cards with real data (keep sparkline as decorative)
- "Nová investice" button → form (name, type, amount, units, price per unit)
- "Prodat" button per investment → SellInvestmentAsync
- Keep gamification/leaderboard section with demo data (no backend for that)

```bash
git commit -m "feat(frontend): connect Investments page to real API endpoints"
```

---

## Task 15: Frontend — Profile Editing

**Files:**
- Modify: `src/FairBank.Web.Profile/Pages/Profile.razor`

**Changes:**
- Add "Změnit" button next to Email → inline edit form with save/cancel
- Add "Změnit heslo" button → expandable form: current password, new password, confirm new password
- Password strength indicator (reuse from Register.razor)
- Call `ChangeEmailAsync` / `ChangePasswordAsync`
- On email change: update `AuthSession` in localStorage (new email)
- Success/error messages

```bash
git commit -m "feat(frontend): add email and password editing to Profile page"
```

---

## Task 16: Frontend — Admin User Management

**Files:**
- Modify: `src/FairBank.Web/Pages/Admin.razor` (create if doesn't exist)

**Note:** The admin page might be in `FairBank.Admin.Web` or `FairBank.Web`. Check which one has the `/admin` route. If it's in `FairBank.Admin.Web`, the API client might not be available there — in that case, add the admin UI to `FairBank.Web` instead (which already has `FairBankApiClient`).

**Admin.razor page** (`@page "/admin"`):
- User table: Name, Email, Role badge, Status badge, CreatedAt, Actions
- Search input (filters by name/email)
- Role dropdown filter (All, Client, Child, Banker, Admin)
- Pagination (20 per page)
- Actions per row:
  - Role dropdown → `UpdateUserRoleAsync`
  - Activate/Deactivate toggle → `ActivateUserAsync`/`DeactivateUserAsync`
  - Delete button (danger, with confirmation) → `DeleteUserAsync`
- Color indicators: green=Active, red=Inactive

```bash
git commit -m "feat(frontend): add admin user management panel"
```

---

## Task 17: Frontend — Child Account UI (/rodina)

**Files:**
- Create: `src/FairBank.Web/Pages/Family.razor`

**Family.razor page** (`@page "/rodina"`):

**Tab "Děti":**
- Load children via `GetChildrenAsync(userId)`
- For each child: load their accounts via `GetAccountsByOwnerAsync(childId)`
- Card per child: name, email, total balance, account status
- "Přidat dítě" button → form: firstName, lastName, email, password, currency, initial spending limit
- Call `CreateChildAsync` then `CreateAccountAsync` for the child

**Tab "Schvalování":**
- For each child's account: `GetPendingTransactionsAsync(accountId)`
- List of pending items: child name, amount, description, date
- Approve button (green) → `ApproveTransactionAsync`
- Reject button (red) → modal with reason → `RejectTransactionAsync`

**Tab "Limity":**
- For each child's account: show current spending limit
- Edit form: new limit amount → `SetSpendingLimitAsync` (reuse existing API method)

**Navigation:** Add "Rodina" link for `Client` role (in SideNav and BottomNav):
```html
@if (Auth.CurrentSession?.Role == "Client")
{
    <NavLink class="side-nav-item" href="rodina">
        <span class="side-nav-icon"><VbIcon Name="users" Size="sm" /></span>
        <span class="side-nav-label">Rodina</span>
    </NavLink>
}
```

Add `users` icon to VbIcon.razor if not present.

```bash
git commit -m "feat(frontend): add Family page with child management, approvals and limits"
```

---

## Task 18: Frontend — Banker Dashboard

**Files:**
- Modify: `src/FairBank.Web.Products/Pages/Management.razor`

**Changes:**
- Add tab navigation: "Žádosti" (existing) | "Klienti" (new) | "Přehled" (new)
- Default tab: "Přehled"

**Tab "Přehled":**
- Stats cards: total clients, active chats, pending applications
- Quick action buttons: "Nepřiřazené chaty", "Pending žádosti"

**Tab "Klienti":**
- Load clients via new Chat API endpoint: `GET /api/v1/chat/conversations/banker/{bankerId}/clients`
- Add method to `ChatService.cs`: `GetBankerClientsAsync(bankerId)`
- Card per client: name, email, active chat count, last activity
- Click → navigate to chat or show detail modal

Keep existing "Žádosti" tab unchanged.

```bash
git commit -m "feat(frontend): add banker dashboard with client overview and stats"
```

---

## Task 19: VbIcon Updates + Navigation Cleanup

**Files:**
- Modify: `src/FairBank.Web.Shared/Components/VbIcon.razor` — add missing icons
- Modify: `src/FairBank.Web/Layout/SideNav.razor` — add Karty and Rodina links
- Modify: `src/FairBank.Web/Layout/BottomNav.razor` — add Karty and Rodina links

**New icons needed:**
- `credit-card` — for Cards page nav
- `users` — for Family page nav (if not already present)
- `chart` or `stats` — for banker overview (if not already present)

**Final nav order:**
1. Přehled (home)
2. Platby (payments)
3. **Karty (credit-card)** ← NEW
4. Spoření (target)
5. Investice (investments)
6. Kurzy (exchange)
7. Produkty (bank) — hidden for Child
8. **Rodina (users)** ← NEW, Client only
9. Zprávy (chat)
10. Správa (clipboard) — Banker only
11. Admin (settings) — Admin only
12. Profil (user)

```bash
git commit -m "feat(frontend): add VbIcon icons and update navigation with Cards and Family links"
```

---

## Task 20: Docker Build + Full Test + Visual Verification

**Steps:**

1. Run all unit tests:
```bash
dotnet test FairBank.slnx -v minimal
```
Expected: All tests pass (existing + new ~30 tests).

2. Build Docker images:
```bash
docker compose build web-app accounts-api identity-api chat-api api-gateway
```

3. Restart services:
```bash
docker compose up -d
```

4. Verify endpoints manually:
```bash
# Cards
curl -s http://localhost:80/api/v1/accounts/{accountId}/cards | jq
# Savings goals
curl -s http://localhost:80/api/v1/accounts/{accountId}/savings-goals | jq
# Investments
curl -s http://localhost:80/api/v1/accounts/{accountId}/investments | jq
# Admin users
curl -s http://localhost:80/api/v1/users?page=1&pageSize=5 | jq
```

5. Visual verification in browser: check all new pages render correctly.

6. Final commit and push:
```bash
git add -A
git commit -m "chore: docker build verification and final cleanup"
git push origin check-project
```

---

## Summary

| Task | Area | Backend/Frontend | Estimated Files |
|------|------|-----------------|-----------------|
| 1 | Card Domain | Backend | 9 files |
| 2 | Card Application | Backend | 17 files |
| 3 | Card Infra + API | Backend | 4 files |
| 4 | SavingsGoal full stack | Backend | 15 files |
| 5 | SavingsRule full stack | Backend | 10 files |
| 6 | Investment full stack | Backend | 15 files |
| 7 | Gateway routes | Config | 1 file |
| 8 | Profile editing | Backend | 8 files |
| 9 | Admin management | Backend | 12 files |
| 10 | Banker clients endpoint | Backend | 1 file |
| 11 | Frontend API client + DTOs | Frontend | 5 files |
| 12 | Cards page | Frontend | 5 files |
| 13 | Savings page update | Frontend | 1 file |
| 14 | Investments page update | Frontend | 1 file |
| 15 | Profile editing UI | Frontend | 1 file |
| 16 | Admin UI | Frontend | 1 file |
| 17 | Child account UI | Frontend | 1 file |
| 18 | Banker dashboard | Frontend | 1 file |
| 19 | VbIcon + Nav updates | Frontend | 3 files |
| 20 | Docker + test | DevOps | 0 files |

**Total: 20 tasks, ~110 files, ~30 new unit tests**
