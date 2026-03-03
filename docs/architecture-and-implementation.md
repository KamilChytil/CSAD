# FairBank — Architektura, implementace a databázová struktura

> Kompletní dokumentace toho, co bylo implementováno, jak to funguje, jaká je struktura databází a workflow celého systému.

---

## 1. Přehled projektu

FairBank je digitální bankovní aplikace postavená jako **mikroservisní architektura** v .NET 10. Projekt se skládá z:

- **Identity Service** — správa uživatelů, registrace, rodičovské/dětské účty (EF Core + PostgreSQL)
- **Accounts Service** — bankovní účty, transakce, spending limity, pending transakce (Marten Event Sourcing + PostgreSQL)
- **API Gateway** — YARP reverse proxy, routuje požadavky na správnou mikroslužbu
- **Blazor WASM Frontend** — SPA klient, micro-frontend architektura s Razor Class Libraries
- **PostgreSQL Primary + Replica** — streaming replikace pro redundanci

### Tech stack

| Technologie | Verze | Účel |
|---|---|---|
| .NET | 10.0 | Runtime |
| EF Core | 10.0.3 | ORM pro Identity service |
| Marten | 8.22.1 | Event Sourcing pro Accounts service |
| PostgreSQL | 16-alpine | Databáze (primary + replica) |
| MediatR | 14.0.0 | CQRS pattern |
| FluentValidation | 12.1.1 | Validace příkazů |
| BCrypt.Net-Next | 4.0.3 | Hashování hesel |
| YARP | 2.3.0 | API Gateway / reverse proxy |
| Blazor WASM | 10.0.3 | Frontend SPA |
| xUnit + NSubstitute | - | Testování |

---

## 2. Architektura — Hexagonální / Clean Architecture

Každá mikroslužba má 4 vrstvy:

```
┌─────────────────────────────────────────┐
│              Api (Endpoints)            │  ← HTTP endpointy (Minimal API)
├─────────────────────────────────────────┤
│         Application (CQRS)              │  ← Commands, Queries, Handlers, DTOs
├─────────────────────────────────────────┤
│             Domain                      │  ← Entity/Aggregate, Value Objects, Events
├─────────────────────────────────────────┤
│          Infrastructure                 │  ← EF Core / Marten, Repositories, DI
└─────────────────────────────────────────┘
```

**Pravidlo závislostí:** Vnější vrstvy závisí na vnitřních. Domain nemá žádné závislosti. Application závisí jen na Domain. Infrastructure implementuje porty z Application.

### CQRS s MediatR

Každá operace je buď **Command** (mutace) nebo **Query** (čtení):

```
HTTP Request → Endpoint → ISender.Send(Command/Query) → Handler → Repository/EventStore → Response
```

Příklad flow pro vytvoření účtu:
1. `POST /api/v1/accounts` přijme `CreateAccountCommand`
2. MediatR dispatchne na `CreateAccountCommandHandler`
3. Handler vytvoří `Account` agregát (domain), který vyemituje `AccountCreated` event
4. `IAccountEventStore.StartStreamAsync()` uloží event do Martenu
5. Handler vrátí `AccountResponse` DTO

---

## 3. Struktura projektu

```
src/
├── FairBank.ApiGateway/            # YARP reverse proxy
├── FairBank.SharedKernel/          # Sdílené abstrakce (AggregateRoot, ValueObject, IUnitOfWork)
├── FairBank.Web/                   # Blazor WASM hostitel
├── FairBank.Web.Auth/              # Login/Register stránky
├── FairBank.Web.Shared/            # Sdílené modely, API klient, AuthService
├── FairBank.Web.Overview/          # Dashboard
├── FairBank.Web.Payments/          # Platby
├── FairBank.Web.Savings/           # Spoření
├── FairBank.Web.Investments/       # Investice
├── FairBank.Web.Profile/           # Profil
└── Services/
    ├── Identity/
    │   ├── FairBank.Identity.Domain/           # User, Email, UserRole
    │   ├── FairBank.Identity.Application/      # Commands, Queries, Handlers
    │   ├── FairBank.Identity.Infrastructure/   # EF Core, Migrations, Repositories
    │   └── FairBank.Identity.Api/              # Endpointy
    └── Accounts/
        ├── FairBank.Accounts.Domain/           # Account, PendingTransaction, Events
        ├── FairBank.Accounts.Application/      # Commands, Queries, Handlers
        ├── FairBank.Accounts.Infrastructure/   # Marten, EventStores
        └── FairBank.Accounts.Api/              # Endpointy

tests/
├── FairBank.Accounts.UnitTests/    # 26 testů
└── FairBank.Identity.UnitTests/    # 24 testů

docker/
└── postgres/
    ├── init.sql                    # Vytvoření schémat a uživatelů
    ├── primary-init.sh             # Konfigurace replikačního uživatele
    └── replica-entrypoint.sh       # Base backup z primary, start hot standby
```

---

## 4. Databázová struktura

### Jedna PostgreSQL databáze, dvě schémata

Obě mikroslužby sdílejí jednu databázi `fairbank`, ale mají **oddělená schémata**:

```
fairbank (databáze)
├── identity_service (schéma)    ← EF Core — relační tabulky
└── accounts_service (schéma)    ← Marten — event sourcing tabulky
```

### 4.1 Identity Service — schéma `identity_service`

Používá **EF Core** s klasickými relačními tabulkami.

#### Tabulka `users`

| Sloupec | Typ | Popis |
|---|---|---|
| `id` | `uuid` PK | Unikátní ID uživatele |
| `first_name` | `varchar(100)` | Křestní jméno |
| `last_name` | `varchar(100)` | Příjmení |
| `email` | `varchar(320)` UNIQUE | Email (owned value object) |
| `password_hash` | `varchar(500)` | BCrypt hash hesla (work factor 12) |
| `role` | `varchar(20)` | `Client`, `Child`, `Banker`, `Admin` |
| `is_active` | `bool` | Je účet aktivní? |
| `is_deleted` | `bool` | Soft delete flag |
| `created_at` | `timestamp` | Datum vytvoření |
| `updated_at` | `timestamp?` | Datum poslední změny |
| `deleted_at` | `timestamp?` | Datum smazání |
| `parent_id` | `uuid?` FK → `users.id` | Rodič (self-reference, jen pro `Child` role) |

**Indexy:**
- PK na `id`
- UNIQUE na `email`
- INDEX na `parent_id`

**Vztahy:**
- `User.Parent` → `User` (many-to-one, self-reference)
- `User.Children` → `List<User>` (one-to-many)
- FK `parent_id` → `users.id` s `ON DELETE RESTRICT`

**Globální query filter:** `WHERE is_deleted = false` — soft delete je automaticky filtrován.

**UserRole enum:**
- `Client` (0) — běžný zákazník
- `Child` (1) — dětský účet s rodičovským dohledem
- `Banker` (2) — bankéř
- `Admin` (3) — administrátor

### 4.2 Accounts Service — schéma `accounts_service`

Používá **Marten Event Sourcing**. Marten automaticky vytváří tyto tabulky:

#### Event Store tabulky (vytvořeno automaticky Martenem)

| Tabulka | Účel |
|---|---|
| `mt_events` | Hlavní tabulka eventů — obsahuje všechny eventy pro všechny streamy |
| `mt_streams` | Metadata o event streamech (aggregate ID, verze, typ) |
| `mt_doc_account` | Inline snapshot — aktuální stav `Account` agregátu |
| `mt_doc_pendingtransaction` | Inline snapshot — aktuální stav `PendingTransaction` agregátu |

#### Account agregát — ukládané eventy

Každý `Account` je event stream. Stav se rekonstruuje aplikací eventů v pořadí:

| Event | Kdy | Data |
|---|---|---|
| `AccountCreated` | Nový účet | AccountId, OwnerId, AccountNumber, Currency, OccurredAt |
| `MoneyDeposited` | Vklad | AccountId, Amount, Currency, Description, OccurredAt |
| `MoneyWithdrawn` | Výběr | AccountId, Amount, Currency, Description, OccurredAt |
| `SpendingLimitSet` | Nastavení limitu | AccountId, Limit, Currency, OccurredAt |

**Account agregát — stav (snapshot):**

| Pole | Typ | Popis |
|---|---|---|
| `Id` | `Guid` | ID účtu (= stream ID) |
| `OwnerId` | `Guid` | ID vlastníka (User ID z Identity service) |
| `AccountNumber` | `AccountNumber` | 10-ciferné číslo účtu |
| `Balance` | `Money` | Aktuální zůstatek (Amount + Currency) |
| `IsActive` | `bool` | Je účet aktivní? |
| `CreatedAt` | `DateTime` | Datum vytvoření |
| `SpendingLimit` | `Money?` | Limit výdajů (null = bez limitu) |
| `RequiresApproval` | `bool` | Vyžaduje schválení transakcí? |
| `ApprovalThreshold` | `Money?` | Hranice, nad kterou je nutné schválení |

#### PendingTransaction agregát — ukládané eventy

| Event | Kdy | Data |
|---|---|---|
| `TransactionRequested` | Dítě chce provést transakci | TransactionId, AccountId, Amount, Currency, Description, RequestedBy, OccurredAt |
| `TransactionApproved` | Rodič schválil | TransactionId, ApproverId, OccurredAt |
| `TransactionRejected` | Rodič zamítl | TransactionId, ApproverId, Reason, OccurredAt |

**PendingTransaction agregát — stav (snapshot):**

| Pole | Typ | Popis |
|---|---|---|
| `Id` | `Guid` | ID transakce (= stream ID) |
| `AccountId` | `Guid` | ID účtu |
| `Amount` | `Money` | Částka |
| `Description` | `string` | Popis transakce |
| `RequestedBy` | `Guid` | ID dítěte, které žádá |
| `ApproverId` | `Guid?` | ID rodiče, který schválil/zamítl |
| `Status` | `PendingTransactionStatus` | `Pending`, `Approved`, `Rejected` |
| `RejectionReason` | `string?` | Důvod zamítnutí |
| `CreatedAt` | `DateTime` | Datum vytvoření |
| `ResolvedAt` | `DateTime?` | Datum rozhodnutí |

### 4.3 Value Objects

**Money** — immutable, zaokrouhluje na 2 desetinná místa, kontroluje záporné hodnoty a shodu měn:
```csharp
Money.Create(500, Currency.CZK)  // → 500.00 CZK
money1.Add(money2)               // → nový Money, kontroluje měnu
money1.Subtract(money2)          // → nový Money, throws pokud nedostatek
```

**Currency enum:** `CZK`, `EUR`, `USD`, `GBP`

**AccountNumber** — generuje náhodné 10-ciferné číslo účtu.

**Email** — validovaný email s regex kontrolou.

---

## 5. PostgreSQL Primary + Replica — Streaming Replikace

### Jak to funguje

```
┌──────────────┐     WAL streaming     ┌──────────────┐
│   PRIMARY    │ ──────────────────────→│   REPLICA    │
│ (read/write) │                        │  (read-only) │
│  port 5432   │                        │  port 5432   │
└──────────────┘                        └──────────────┘
```

1. **Primary** přijímá všechny zápisy (INSERT, UPDATE, DELETE)
2. Write-Ahead Log (WAL) se streamuje do **Replica**
3. Replica je v režimu **hot standby** — přijímá read-only dotazy
4. Pokud primary selže, replica má kopii dat

### Inicializace

**Primary (primary-init.sh):**
1. Vytvoří uživatele `replicator` s oprávněním `REPLICATION`
2. Přidá řádek do `pg_hba.conf` povolující replikační připojení
3. Reloadne PostgreSQL konfiguraci

**Primary PostgreSQL parametry (docker-compose):**
- `wal_level=replica` — plný WAL pro replikaci
- `max_wal_senders=3` — max 3 replikační připojení
- `wal_keep_size=64MB` — kolik WAL segmentů uchovat
- `hot_standby=on` — povolí read-only dotazy na standby

**Replica (replica-entrypoint.sh):**
1. Čeká, až bude primary ready (`pg_isready`)
2. Provede `pg_basebackup` — zkopíruje celý datový adresář z primary
3. Vytvoří `standby.signal` soubor — PostgreSQL ví, že je replica
4. Spustí PostgreSQL v hot standby režimu

**Init SQL (init.sql):**
1. Vytvoří aplikačního uživatele `fairbank_app`
2. Vytvoří schémata `identity_service` a `accounts_service`
3. Nastaví oprávnění (GRANT ALL na schémata, tabulky, sekvence)

### Uživatelé databáze

| Uživatel | Heslo | Účel |
|---|---|---|
| `fairbank_admin` | `fairbank_secret_2026` | Superuser, owner databáze |
| `fairbank_app` | `fairbank_app_2026` | Aplikační uživatel (služby se připojují jako tento) |
| `replicator` | `replicator_2026` | Replikační uživatel (jen pro streaming) |

---

## 6. API Endpointy

### API Gateway routing (YARP)

```
Frontend (port 80)  →  API Gateway (port 8080)  →  Správná služba
```

| Pattern | Cíl |
|---|---|
| `/api/v1/users/**` | `identity-api:8080` |
| `/api/v1/accounts/**` | `accounts-api:8080` |

### Identity Service — UserEndpoints

| Metoda | Path | Handler | Popis |
|---|---|---|---|
| `POST` | `/api/v1/users/register` | `RegisterUserCommand` | Registrace nového uživatele |
| `GET` | `/api/v1/users/{id}` | `GetUserByIdQuery` | Získat uživatele podle ID |
| `POST` | `/api/v1/users/{parentId}/children` | `CreateChildCommand` | Vytvořit dětský účet |
| `GET` | `/api/v1/users/{parentId}/children` | `GetChildrenQuery` | Získat seznam dětí rodiče |

### Accounts Service — AccountEndpoints

| Metoda | Path | Handler | Popis |
|---|---|---|---|
| `POST` | `/api/v1/accounts` | `CreateAccountCommand` | Vytvořit bankovní účet |
| `GET` | `/api/v1/accounts/{id}` | `GetAccountByIdQuery` | Získat účet podle ID |
| `POST` | `/api/v1/accounts/{id}/deposit` | `DepositMoneyCommand` | Vklad na účet |
| `POST` | `/api/v1/accounts/{id}/withdraw` | `WithdrawMoneyCommand` | Výběr z účtu |
| `POST` | `/api/v1/accounts/{id}/limits` | `SetSpendingLimitCommand` | Nastavit spending limit |
| `GET` | `/api/v1/accounts/{id}/pending` | `GetPendingTransactionsQuery` | Pending transakce účtu |
| `POST` | `/api/v1/accounts/pending/{id}/approve` | `ApproveTransactionCommand` | Schválit transakci |
| `POST` | `/api/v1/accounts/pending/{id}/reject` | `RejectTransactionCommand` | Zamítnout transakci |

### Stav autentizace

**Soft auth** — žádný endpoint nemá `.RequireAuthorization()`. Všechny endpointy jsou volně přístupné pro snadné testování. Frontend posílá tokeny, ale backend je nekontroluje. Auth backend (JWT, Session, RefreshToken) bude implementován v budoucí fázi.

---

## 7. Workflow dětských účtů a rodičovského dohledu

### Vytvoření dětského účtu

```
1. Rodič (Client) existuje v systému
2. POST /api/v1/users/{parentId}/children
   → CreateChildCommandHandler:
     a) Ověří, že parent existuje a má roli Client
     b) Ověří, že email dítěte je unikátní
     c) Hashuje heslo BCryptem (work factor 12)
     d) User.CreateChild() → nastaví ParentId a roli Child
     e) Uloží přes UserRepository + UnitOfWork
3. Dítě je vytvořeno s rolí Child a FK na rodiče
```

### Spending limity a schvalování transakcí

```
1. Rodič nastaví limit na účtu dítěte:
   POST /api/v1/accounts/{id}/limits  { Limit: 500, Currency: "CZK" }
   → Account.SetSpendingLimit(Money) → SpendingLimitSet event

2. Dítě chce provést transakci nad limit:
   → Account.NeedsApproval(amount) vrátí true
   → Vytvoří se PendingTransaction (TransactionRequested event)
   → Peníze se NESTRHNOU, čeká se na rodiče

3. Rodič schválí:
   POST /api/v1/accounts/pending/{txId}/approve { ApproverId: parentId }
   → PendingTransaction.Approve() → TransactionApproved event
   → Provede se skutečný Withdraw na účtu → MoneyWithdrawn event

4. Nebo rodič zamítne:
   POST /api/v1/accounts/pending/{txId}/reject { ApproverId: parentId, Reason: "..." }
   → PendingTransaction.Reject() → TransactionRejected event
   → Peníze se nestrhnou
```

---

## 8. Event Sourcing — jak funguje v Accounts service

### Princip

Místo ukládání aktuálního stavu (jako EF Core) ukládáme **sled událostí** (events). Stav se kdykoli rekonstruuje přehráním eventů.

```
Events:  AccountCreated → MoneyDeposited(1000) → MoneyWithdrawn(300) → MoneyDeposited(500)
State:   Balance = 0    → Balance = 1000       → Balance = 700       → Balance = 1200
```

### Marten implementace

- **StartStream** — vytvoří nový event stream (pro nové agregáty)
- **Append** — přidá eventy do existujícího streamu (pro operace na existujících agregátech)
- **AggregateStreamAsync** — načte stream a přehraje eventy přes `Apply()` metody
- **Inline Snapshot** — Marten automaticky udržuje snapshot (aktuální stav) v `mt_doc_*` tabulkách

### Proč StartStream vs Append?

Marten rozlišuje **vytvoření nového streamu** (`StartStream`) od **přidání do existujícího** (`Append`). Pokud použijete `Append` na neexistující stream, Marten hodí výjimku. Proto máme v `IAccountEventStore` dvě metody:

```csharp
Task StartStreamAsync(Account account, CancellationToken ct);   // pro CreateAccount
Task AppendEventsAsync(Account account, CancellationToken ct);   // pro Deposit, Withdraw, SetLimit
```

---

## 9. Frontend — API klient

Frontend komunikuje s backendem přes `IFairBankApi` rozhraní implementované v `FairBankApiClient`:

```csharp
public interface IFairBankApi
{
    // Accounts
    Task<AccountResponse?> GetAccountAsync(Guid id);
    Task<AccountResponse> CreateAccountAsync(Guid ownerId, string currency);
    Task<AccountResponse> DepositAsync(Guid accountId, decimal amount, string currency, string? description);
    Task<AccountResponse> WithdrawAsync(Guid accountId, decimal amount, string currency, string? description);

    // Users
    Task<UserResponse?> GetUserAsync(Guid id);
    Task<UserResponse> RegisterUserAsync(string firstName, string lastName, string email, string password);

    // Auth (frontend posílá, backend zatím neodpovídá — soft auth)
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task LogoutAsync(string token);
    Task<UserResponse?> RegisterExtendedAsync(RegisterRequest request);
    Task<bool> ValidateSessionAsync(Guid sessionId, string token);

    // Children
    Task<List<UserResponse>> GetChildrenAsync(Guid parentId);
    Task<UserResponse> CreateChildAsync(Guid parentId, string firstName, string lastName, string email, string password);

    // Account queries
    Task<List<AccountResponse>> GetAccountsByOwnerAsync(Guid ownerId);

    // Pending transactions
    Task<List<PendingTransactionDto>> GetPendingTransactionsAsync(Guid accountId);
    Task<PendingTransactionDto> ApproveTransactionAsync(Guid transactionId, Guid approverId);
    Task<PendingTransactionDto> RejectTransactionAsync(Guid transactionId, Guid approverId, string reason);
}
```

---

## 10. Docker Compose — celý stack

```
                    ┌──────────────┐
                    │  web-app:80  │  ← Blazor WASM (jediný veřejný port)
                    └──────┬───────┘
                           │
                    ┌──────▼───────┐
                    │ api-gateway  │  ← YARP reverse proxy (port 8080 interní)
                    │   :8080      │
                    └──┬───────┬───┘
                       │       │
              ┌────────▼──┐ ┌──▼────────┐
              │identity-api│ │accounts-api│  ← Mikroslužby (port 8080 interní)
              │   :8080    │ │   :8080    │
              └────┬───────┘ └────┬──────┘
                   │              │
              ┌────▼──────────────▼──────┐
              │     postgres-primary     │  ← PostgreSQL 16 (port 5432 interní)
              │        :5432             │
              └──────────┬───────────────┘
                         │ WAL streaming
              ┌──────────▼───────────────┐
              │     postgres-replica      │  ← Hot standby (read-only)
              │        :5432             │
              └──────────────────────────┘
```

Všechny kontejnery jsou na privátní síti `backend`. Pouze `web-app` má port mapovaný na host (port 80).

---

## 11. Testování

**50 unit testů** celkem (26 Accounts + 24 Identity).

### Accounts testy (26)
- `AccountTests` — Create, Deposit, Withdraw, Deactivate, SpendingLimit, NeedsApproval
- `MoneyTests` — Create, Add, Subtract, currency mismatch, negative amount
- `PendingTransactionTests` — Create, Approve, Reject, double-approve
- `CreateAccountCommandHandlerTests` — valid creation, mock verification
- `DepositMoneyCommandHandlerTests` — valid deposit, account not found
- `WithdrawMoneyCommandHandlerTests` — valid withdrawal, account not found

### Identity testy (24)
- `UserTests` — Create, soft delete, restore, CreateChild
- `EmailTests` — valid/invalid email formats
- `RegisterUserCommandHandlerTests` — valid registration, duplicate email
- `CreateChildCommandHandlerTests` — valid parent, non-client parent

### Spuštění testů

```bash
dotnet test FairBank.slnx
```

---

## 12. EF Core Migrace (Identity)

| Migrace | Popis |
|---|---|
| `20260303101715_InitialCreate` | Základní schéma — tabulka `users` se všemi sloupci, email index, soft delete filter |
| `20260303130235_AddParentChildRelationship` | Přidán `parent_id` sloupec, FK na `users.id`, index na `parent_id` |

### Spuštění migrací

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef database update \
  --project src/Services/Identity/FairBank.Identity.Infrastructure \
  --startup-project src/Services/Identity/FairBank.Identity.Api
```

---

## 13. Centrální správa balíčků

Všechny NuGet verze jsou definovány v `Directory.Packages.props` v rootu. Projekty používají `<PackageReference Include="..." />` bez verzí — verze se berou centrálně.

Hlavní balíčky:
- **MediatR 14.0.0** — CQRS dispatcher
- **FluentValidation 12.1.1** — command validace
- **EF Core 10.0.3** — ORM (Identity)
- **Marten 8.22.1** — Event Sourcing (Accounts)
- **BCrypt.Net-Next 4.0.3** — hashování hesel
- **YARP 2.3.0** — reverse proxy
- **Serilog 10.0.0** — strukturované logování
- **xUnit + FluentAssertions + NSubstitute** — testování

---

## 14. Co NENÍ implementováno (deferred)

Následující bylo záměrně vynecháno pro zjednodušení testování:

- **JWT autentizace** — žádná generace/validace tokenů
- **Session / RefreshToken** — entity existují jen ve frontend modelu
- **Auth endpointy** — login/logout/register na backendu neexistují (frontend dostane 404)
- **Gateway security** — žádné JWT middleware, RBAC, rate limiting, CORS
- **Audit log** — žádné logování operací
- **Authorization na endpointech** — žádné `.RequireAuthorization()`

Toto vše bude přidáno v budoucí fázi. Aktuálně je systém v režimu **"soft auth"** — frontend posílá tokeny, backend je ignoruje.

---

## 15. Git historie (branch `feature/db-redundancy-children-spending`)

| Commit | Popis |
|---|---|
| `3b651f0` | fix: separate StartStream and AppendToStream in Marten event store |
| `8745457` | feat(infra): add PostgreSQL primary-replica streaming replication + BCrypt |
| `98ff56f` | feat(identity): add ParentId self-reference FK on User for child accounts |
| `ac62858` | feat(identity): add child account creation and listing endpoints |
| `69e45f4` | feat(accounts): add spending limits and approval threshold to Account aggregate |
| `b1f25a8` | feat(accounts): add PendingTransaction aggregate, spending limit commands, and approval endpoints |
| `e7a68a1` | feat(frontend): extend IFairBankApi with children and pending transaction methods |
