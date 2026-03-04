# Feature: Complete Features — Cards, Savings, Investments, Profile, Admin, Family, Banker Dashboard

> Date: 2026-03-04
> Tasks: 20 (all completed)
> Tests: 308 passed, 2 pre-existing failures
> Docker: All images rebuilt and passing

---

## What Was Built

7 feature areas implemented to reach 100% specification coverage for all 4 user roles (Client, Child, Banker, Admin).

### 1. Payment Cards (Accounts Service — Event-Sourced, Marten)

Full card management simulation (no real payment processor).

**Domain** (`src/Services/Accounts/FairBank.Accounts.Domain/`)
- `Aggregates/Card.cs` — Event-sourced aggregate with: Id, AccountId, CardNumber (Visa-format `4XXX XXXX XXXX XXXX`), HolderName, ExpirationDate, CVV, Type (Debit/Credit), IsActive, IsFrozen, DailyLimit/MonthlyLimit (Money?), OnlinePaymentsEnabled, ContactlessEnabled
- Methods: `Create()` (generates number + CVV), `Freeze()`, `Unfreeze()`, `SetLimits()`, `UpdateSettings()`, `Deactivate()`
- `MaskedNumber` property returns `**** **** **** 1234`
- `Enums/CardType.cs` — Debit=0, Credit=1
- 6 domain events: CardIssued, CardFrozen, CardUnfrozen, CardLimitSet, CardSettingsChanged, CardDeactivated

**Application** (`src/Services/Accounts/FairBank.Accounts.Application/`)
- `DTOs/CardResponse.cs`
- `Ports/ICardEventStore.cs` — LoadAsync, LoadByAccountAsync, StartStreamAsync, AppendEventsAsync
- Commands: IssueCard, FreezeCard, UnfreezeCard, SetCardLimits, UpdateCardSettings, DeactivateCard
- Query: GetCardsByAccount

**Infrastructure** (`src/Services/Accounts/FairBank.Accounts.Infrastructure/`)
- `Persistence/MartenCardEventStore.cs`

**API** (`src/Services/Accounts/FairBank.Accounts.Api/`)
- `Endpoints/CardEndpoints.cs` — 7 endpoints:
  - `POST /api/v1/accounts/{id}/cards` → 201
  - `GET /api/v1/accounts/{id}/cards` → 200
  - `POST /api/v1/cards/{id}/freeze` → 204
  - `POST /api/v1/cards/{id}/unfreeze` → 204
  - `PUT /api/v1/cards/{id}/limits` → 204
  - `PUT /api/v1/cards/{id}/settings` → 204
  - `DELETE /api/v1/cards/{id}` → 204

**Tests:** 10 domain tests (`CardTests.cs`), 2 handler tests (`IssueCardCommandHandlerTests.cs`)

**Frontend** (`src/FairBank.Web.Cards/`)
- New Razor class library project
- `Pages/Cards.razor` — route `/karty`
- Card list with dark gradient visuals, freeze/unfreeze toggles, limit editing, online/contactless toggles, issue new card form, deactivate with confirmation

---

### 2. Savings Backend + Frontend (Accounts Service — Event-Sourced, Marten)

Replaced demo data with real event-sourced backend.

**SavingsGoal Aggregate** (`src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/SavingsGoal.cs`)
- Properties: Id, AccountId, Name, Description, TargetAmount (Money), CurrentAmount (Money), IsCompleted, CreatedAt, CompletedAt
- Methods: `Create()`, `Deposit()` (auto-completes when target reached), `Withdraw()`
- `ProgressPercent` computed property clamped 0–100
- 4 events: SavingsGoalCreated, SavingsDeposited, SavingsWithdrawn, SavingsGoalCompleted

**SavingsRule Aggregate** (`src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/SavingsRule.cs`)
- Properties: Id, AccountId, Name, Description, Type (SavingsRuleType), Amount, IsEnabled, CreatedAt
- Methods: `Create()` (defaults IsEnabled=true), `Toggle()`
- `Enums/SavingsRuleType.cs` — RoundUp=0, FixedWeekly=1, FixedMonthly=2, PercentageOfIncome=3
- 2 events: SavingsRuleCreated, SavingsRuleToggled

**Application Layer:**
- DTOs: `SavingsGoalResponse.cs`, `SavingsRuleResponse.cs`
- Ports: `ISavingsGoalEventStore.cs`, `ISavingsRuleEventStore.cs`
- Commands: CreateSavingsGoal, DepositToSavingsGoal, WithdrawFromSavingsGoal, DeleteSavingsGoal, CreateSavingsRule, ToggleSavingsRule
- Queries: GetSavingsGoalsByAccount, GetSavingsRulesByAccount

**Infrastructure:** `MartenSavingsGoalEventStore.cs`, `MartenSavingsRuleEventStore.cs`

**API Endpoints:**
- `SavingsGoalEndpoints.cs` — 5 endpoints (POST create, GET list, POST deposit, POST withdraw, DELETE)
- `SavingsRuleEndpoints.cs` — 3 endpoints (POST create, GET list, PUT toggle)

**Tests:** 7 SavingsGoal domain tests, 4 SavingsRule domain tests

**Frontend** (`src/FairBank.Web.Savings/Pages/Savings.razor`) — Updated:
- Replaced demo data with real API calls
- Create goal form (name, description, target amount, currency)
- Progress bar with real data (currentAmount / targetAmount)
- Deposit/withdraw buttons
- Rule toggles (real API), create rule form

---

### 3. Investments Backend + Frontend (Accounts Service — Event-Sourced, Marten)

Replaced demo data with real event-sourced backend.

**Investment Aggregate** (`src/Services/Accounts/FairBank.Accounts.Domain/Aggregates/Investment.cs`)
- Properties: Id, AccountId, Name, Type (InvestmentType), InvestedAmount (Money), CurrentValue (Money), Units, PricePerUnit, IsActive, CreatedAt, SoldAt
- Methods: `Create()`, `UpdateValue(newPricePerUnit)` — recalculates CurrentValue=Units*price, `Sell()`
- `ChangePercent` computed property
- `Enums/InvestmentType.cs` — Stock=0, Bond=1, Crypto=2, Fund=3
- 3 events: InvestmentCreated, InvestmentValueUpdated, InvestmentSold

**Application Layer:**
- DTO: `InvestmentResponse.cs`
- Port: `IInvestmentEventStore.cs`
- Commands: CreateInvestment, UpdateInvestmentValue, SellInvestment
- Queries: GetInvestmentsByAccount, GetInvestmentById

**Infrastructure:** `MartenInvestmentEventStore.cs`

**API Endpoints** (`InvestmentEndpoints.cs`) — 5 endpoints:
- `POST /api/v1/accounts/{id}/investments` → 201
- `GET /api/v1/accounts/{id}/investments` → 200
- `GET /api/v1/investments/{id}` → 200
- `PUT /api/v1/investments/{id}/value` → 204
- `POST /api/v1/investments/{id}/sell` → 204

**Seeder** (`AccountSeeder.cs`) — 3 demo investments: Akciový fond (Stock), Dluhopisový fond (Bond), Kryptoměny (Crypto)

**Tests:** 6 Investment domain tests

**Frontend** (`src/FairBank.Web.Investments/Pages/Investments.razor`) — Updated:
- Replaced demo data with real API calls
- Portfolio overview (total value, change %)
- Buy/sell forms with confirmation
- Kept gamification/sparkline as demo visualization

---

### 4. Profile Editing (Identity Service)

**Backend:**
- `ChangeEmailCommand(UserId, NewEmail)` + handler + FluentValidation validator (format + uniqueness)
- `ChangePasswordCommand` already existed, only endpoint was added
- `User.ChangeEmail(newEmail)` domain method added
- New endpoints:
  - `PUT /api/v1/users/{id}/email` → 204
  - `PUT /api/v1/users/{id}/password` → 204

**Tests:** 3 ChangeEmail handler tests

**Frontend** (`src/FairBank.Web.Profile/Pages/Profile.razor`) — Updated:
- Email change inline form
- Password change expandable form with strength indicator (8+ chars, uppercase, lowercase, digit, special char)
- Post-change: updates AuthSession in localStorage

---

### 5. Admin User Management (Identity Service)

**Backend:**
- Domain methods on `User`: `ChangeRole(newRole)`, `Activate()`, `Deactivate()` (SoftDelete already existed)
- `PagedUsersResponse(Items, TotalCount, Page, PageSize)` DTO
- `GetAllUsersQuery(Page, PageSize, RoleFilter?, SearchTerm?)` — paginated with EF Core filters
- Commands: UpdateUserRole, DeactivateUser, ActivateUser, DeleteUser (soft delete)
- New endpoints:
  - `GET /api/v1/users?page=1&pageSize=20&role=Client&search=novak` → 200
  - `PUT /api/v1/users/{id}/role` → 204
  - `POST /api/v1/users/{id}/deactivate` → 204
  - `POST /api/v1/users/{id}/activate` → 204
  - `DELETE /api/v1/users/{id}` → 204

**Tests:** 5 admin command handler tests

**Frontend** (`src/FairBank.Web/Pages/Admin.razor`) — Replaced placeholder:
- User table with columns: Jméno, Email, Role, Status, Datum registrace, Akce
- Search input + role filter dropdown
- Pagination (20 per page)
- Inline actions: role change dropdown, activate/deactivate toggle, delete with confirmation
- Color indicators: green (Active), red (Inactive)

---

### 6. Child Account Frontend (/rodina)

Backend already existed. This added the UI.

**Frontend** (`src/FairBank.Web/Pages/Family.razor`) — New page, route `/rodina`:
- **Tab "Děti":** List of children (name, email, balance, status), "Přidat dítě" button → CreateChildForm (name, surname, email, password, currency, spending limit)
- **Tab "Schvalování":** Pending transactions from all children, approve (green) / reject (red with reason) buttons
- **Tab "Limity":** Per-child spending limit and approval threshold editing

Only visible to `Client` role.

---

### 7. Banker Dashboard (Management.razor Enhancement)

**Backend** (Chat Service):
- New endpoint: `GET /api/v1/chat/conversations/banker/{bankerId}/clients`
- Groups conversations by ClientOrChildId, returns: ClientId, ClientName, ActiveChatsCount, LastActivity
- Filter: `Type == ConversationType.Support` (excludes family chats)

**Frontend** (`src/FairBank.Web.Products/Pages/Management.razor`) — Added tabs:
- **Tab "Přehled":** Stats cards (total clients, active chats, pending requests), quick action links
- **Tab "Žádosti":** Existing product approval functionality (unchanged)
- **Tab "Klienti":** Client list from chat API, click to see details

**New DTOs:** `BankerClientDto.cs` in Web.Shared, `GetBankerClientsAsync` in API client

---

## Gateway Routes Added

4 new YARP routes in `src/FairBank.ApiGateway/appsettings.json`:

| Route | Pattern | Cluster |
|-------|---------|---------|
| `cards-route` | `/api/v1/cards/{**catch-all}` | `accounts-cluster` |
| `savings-goals-route` | `/api/v1/savings-goals/{**catch-all}` | `accounts-cluster` |
| `savings-rules-route` | `/api/v1/savings-rules/{**catch-all}` | `accounts-cluster` |
| `investments-route` | `/api/v1/investments/{**catch-all}` | `accounts-cluster` |

Account-level endpoints (`/api/v1/accounts/{id}/cards`, `/api/v1/accounts/{id}/savings-goals`, etc.) are covered by existing `accounts-route`.

---

## Frontend API Client Updates

**New DTOs** in `src/FairBank.Web.Shared/Models/`:
- `CardDto.cs`
- `PagedUsersDto.cs` + `UserResponseDto`
- `BankerClientDto.cs`
- Updated: `SavingsGoalDto.cs`, `SavingsRuleDto.cs`, `InvestmentDto.cs`

**25+ new methods** in `IFairBankApi` / `FairBankApiClient`:
- Cards: GetCards, IssueCard, FreezeCard, UnfreezeCard, SetCardLimits, UpdateCardSettings, DeactivateCard
- Savings: GetSavingsGoals, CreateSavingsGoal, DepositToSavingsGoal, WithdrawFromSavingsGoal, DeleteSavingsGoal, GetSavingsRules, CreateSavingsRule, ToggleSavingsRule
- Investments: GetInvestments, GetInvestment, CreateInvestment, UpdateInvestmentValue, SellInvestment
- Admin: GetAllUsers, UpdateUserRole, DeactivateUser, ActivateUser, DeleteUser
- Profile: ChangeEmail, ChangePassword
- Banker: GetBankerClients

---

## Navigation Updates

**VbIcon** (`src/FairBank.Web.Shared/Components/VbIcon.razor`):
- Added `credit-card` icon (SVG)
- Added `users` icon (SVG)

**SideNav** (`src/FairBank.Web/Layout/SideNav.razor`):
- Added "Karty" link (credit-card icon) between Platby and Spoření
- Added "Rodina" link (users icon) — visible only for `Client` role

**BottomNav** (`src/FairBank.Web/Layout/BottomNav.razor`):
- Same additions as SideNav

**Final navigation order (all roles):**
Přehled → Platby → **Karty** → Spoření → Investice → Kurzy → Produkty → *Rodina (Client only)* → Zprávy → *Správa (Banker)* / *Admin (Admin)* → Profil

---

## Marten Configuration Updates

`src/Services/Accounts/FairBank.Accounts.Infrastructure/DependencyInjection.cs`:
- Added 4 inline snapshot projections: Card, SavingsGoal, SavingsRule, Investment
- Added 4 scoped DI registrations for event stores

---

## Solution/Project Changes

- `FairBank.slnx` — Added `FairBank.Web.Cards` project
- `src/FairBank.Web/FairBank.Web.csproj` — Added reference to `FairBank.Web.Cards`
- `src/FairBank.Web.Cards/FairBank.Web.Cards.csproj` — New Razor class library

---

## Test Summary

| Test Project | New Tests | Total |
|-------------|-----------|-------|
| `FairBank.Accounts.UnitTests` — CardTests | 10 | — |
| `FairBank.Accounts.UnitTests` — IssueCardCommandHandlerTests | 2 | — |
| `FairBank.Accounts.UnitTests` — SavingsGoalTests | 7 | — |
| `FairBank.Accounts.UnitTests` — SavingsRuleTests | 4 | — |
| `FairBank.Accounts.UnitTests` — InvestmentTests | 6 | — |
| `FairBank.Identity.UnitTests` — ChangeEmailCommandHandlerTests | 3 | — |
| `FairBank.Identity.UnitTests` — AdminCommandsTests | 5 | — |
| **Total new** | **37** | — |
| **Total suite** | — | **308 passed, 2 pre-existing failures** |

**Pre-existing failures (not caused by this work):**
1. `CreateAccountCommandHandlerTests.Handle_WithValidCommand_ShouldCreateAccountWithZeroBalance` — expects `FAIR-` prefix but actual format is Czech `000000-XXXXXXXXXX/8888`
2. `InsuranceCalculatorTests.CalculateLife_Age30_1M_Risk_ReturnsReasonableAmount` — expects monthly 200–500 but calculation returns 25

---

## Design Documents

- `docs/plans/2026-03-04-complete-features-design.md` — Full design spec for all 7 features
- `docs/plans/2026-03-04-complete-features-plan.md` — 20-task implementation plan

---

## Key Implementation Notes

1. **All new Accounts aggregates follow Marten event-sourcing pattern:** sealed class, `[JsonInclude]` properties with private setters, `[JsonConstructor]` private constructor, `_uncommittedEvents` list, static `Create()` factory, `Apply()` methods per event
2. **Cards use simulated Visa numbers** — generated via `Random.Shared`, no real payment processor
3. **SavingsGoal auto-completes** when deposit brings CurrentAmount ≥ TargetAmount
4. **Investment.ChangePercent** is computed: `(CurrentValue - InvestedAmount) / InvestedAmount * 100`
5. **Chat entity property names differ from typical naming:** `BankerOrParentId` (not AssignedBankerId), `ClientOrChildId` (not ClientId), `Label` (not ClientLabel), `LastClientMessageAt`
6. **Identity password hashing uses BCrypt** (workFactor 12), not SHA256
7. **Admin endpoints use `IgnoreQueryFilters()`** to include soft-deleted users in listings
