# Payments Service — Popis implementace

> **Datum:** 2026-03-03  
> **Služba:** Payments Service + FairBank.Web.Payments frontend

---

## Přehled

Kompletní mikroslužba pro platební operace v aplikaci FairBank. Systém zahrnuje:

1. **Standardní platby** — odeslání platby na číslo účtu
2. **Okamžité platby** — zpracování ihned (flag `IsInstant`)
3. **Interní převody** — automatická detekce převodu mezi vlastními účty
4. **Trvalé příkazy** — opakované platby s nastavitelným intervalem
5. **Platební šablony** — uložení často používaných plateb pro rychlé vyplnění

---

## Architektura

Hexagonální architektura shodná s ostatními službami:

```
FairBank.Payments.Domain            → Entity, Value Objects, Enums, Porty (repositáře)
FairBank.Payments.Application       → CQRS příkazy/dotazy (MediatR), validace (FluentValidation)
FairBank.Payments.Infrastructure    → EF Core DbContext, repozitáře, HTTP klient pro Accounts
FairBank.Payments.Api               → Minimal API endpointy, Serilog, Scalar
FairBank.Web.Payments               → Blazor WASM stránka (Payments.razor)
FairBank.Web.Shared                 → Sdílené DTO (PaymentDto, StandingOrderDto, PaymentTemplateDto)
```

**Persistence:** EF Core + PostgreSQL (schema `payments_service`), auto-create přes `EnsureCreatedAsync()`.  
**Cross-service komunikace:** HTTP klient na Accounts API pro ověření účtů a provedení withdraw/deposit.

---

## Domain layer

### Entity: `Payment`

| Vlastnost | Typ | Popis |
|-----------|-----|-------|
| `SenderAccountId` | `Guid` | ID odesílatelova účtu |
| `RecipientAccountId` | `Guid?` | ID příjemcova účtu (null = externí) |
| `SenderAccountNumber` | `string` | Číslo účtu odesílatele (FAIR-XXXX) |
| `RecipientAccountNumber` | `string` | Číslo účtu příjemce |
| `Amount` / `Currency` | `decimal` / `string` | Částka a měna |
| `Description` | `string?` | Volitelný popis |
| `Type` | `PaymentType` | Standard / Instant / StandingOrder / InternalTransfer |
| `Status` | `PaymentStatus` | Pending → Completed / Failed / Cancelled |

**Stavový automat:**
```
Pending ──→ Completed   (MarkCompleted)
Pending ──→ Failed      (MarkFailed + reason)
Pending ──→ Cancelled   (Cancel)
```

### Entity: `StandingOrder`

| Vlastnost | Typ | Popis |
|-----------|-----|-------|
| `SenderAccountId` | `Guid` | ID účtu odesílatele |
| `RecipientAccountNumber` | `string` | Číslo účtu příjemce |
| `Amount` / `Currency` | `decimal` / `string` | Částka a měna |
| `RecurrenceInterval` | enum | Daily / Weekly / Monthly / Quarterly / Yearly |
| `NextExecutionDate` | `DateTime` | Datum příští platby |
| `EndDate` | `DateTime?` | Volitelné datum ukončení |
| `IsActive` | `bool` | Aktivní / zrušený |
| `ExecutionCount` | `int` | Počet provedených plateb |

Metody: `Create()`, `RecordExecution()`, `Deactivate()`, `Activate()`, `Update()`, `IsDueForExecution()`

### Entity: `PaymentTemplate`

| Vlastnost | Typ | Popis |
|-----------|-----|-------|
| `OwnerAccountId` | `Guid` | Účet vlastníka šablony |
| `Name` | `string` | Název šablony (např. "Nájem") |
| `RecipientAccountNumber` | `string` | Číslo účtu příjemce |
| `RecipientName` | `string?` | Jméno příjemce |
| `DefaultAmount` | `decimal?` | Výchozí částka |
| `DefaultDescription` | `string?` | Výchozí popis |
| `IsDeleted` | `bool` | Soft-delete flag |

### Value Object: `Money`

Record s `Amount` (decimal) + `Currency` (string). Factory metody `Create()` a `Zero()`.

### Enums

- `PaymentType`: Standard, Instant, StandingOrder, InternalTransfer
- `PaymentStatus`: Pending, Completed, Failed, Cancelled
- `RecurrenceInterval`: Daily, Weekly, Monthly, Quarterly, Yearly
- `Currency`: CZK, EUR, USD, GBP

---

## Application layer

### Porty

`IAccountsServiceClient` — rozhraní pro HTTP komunikaci s Accounts service:
- `GetAccountByIdAsync(Guid)` → `AccountInfo?`
- `GetAccountByNumberAsync(string)` → `AccountInfo?`
- `WithdrawAsync(Guid, decimal, string, string?)` → `bool`
- `DepositAsync(Guid, decimal, string, string?)` → `bool`

### Commands

#### `SendPaymentCommand`

```csharp
public sealed record SendPaymentCommand(
    Guid SenderAccountId, string RecipientAccountNumber,
    decimal Amount, string Currency,
    string? Description, bool IsInstant) : IRequest<PaymentResponse>;
```

**Flow:**
1. Ověří odesílatelův účet přes `IAccountsServiceClient`
2. Vyhledá příjemce podle čísla účtu
3. Automaticky detekuje interní převod (stejný vlastník) → `InternalTransfer`
4. Provede `WithdrawAsync` z odesílatele
5. Provede `DepositAsync` příjemci
6. Při selhání depositu → **kompenzační transakce** (vrátí peníze odesílateli)
7. Uloží `Payment` entitu se správným statusem

**Validace** (`SendPaymentCommandValidator`):
- `SenderAccountId` — NotEmpty
- `RecipientAccountNumber` — NotEmpty
- `Amount` — GreaterThan(0)
- `Currency` — NotEmpty

#### `CreateStandingOrderCommand`

Vytvoří nový trvalý příkaz s nastavením intervalu, prvního data provedení a volitelného data ukončení.

#### `CancelStandingOrderCommand`

Deaktivuje existující trvalý příkaz (soft-deactivate, `IsActive = false`).

#### `CreateTemplateCommand` / `DeleteTemplateCommand`

CRUD operace nad platebními šablonami. Delete je soft-delete (`IsDeleted = true`).

### Queries

| Query | Vrací | Popis |
|-------|-------|-------|
| `GetPaymentsByAccountQuery` | `List<PaymentResponse>` | Historie plateb (limit, řazení dle data) |
| `GetStandingOrdersByAccountQuery` | `List<StandingOrderResponse>` | Trvalé příkazy účtu |
| `GetTemplatesByAccountQuery` | `List<PaymentTemplateResponse>` | Šablony vlastníka |

---

## Infrastructure layer

### `PaymentsDbContext`

- Schema: `payments_service`
- DbSets: `Payments`, `StandingOrders`, `PaymentTemplates`
- Implementuje `IUnitOfWork`

### EF Core konfigurace

| Entita | Indexy | Speciální |
|--------|--------|-----------|
| `Payment` | SenderAccountId, RecipientAccountId, CreatedAt desc, Status | — |
| `StandingOrder` | Composite: IsActive + NextExecutionDate | — |
| `PaymentTemplate` | OwnerAccountId | Global query filter `!IsDeleted` |

### `AccountsServiceHttpClient`

HTTP klient implementující `IAccountsServiceClient`. Volá REST endpointy Accounts API:
- `GET /api/v1/accounts/{id}`
- `GET /api/v1/accounts/by-number/{accountNumber}`
- `POST /api/v1/accounts/{id}/deposit`
- `POST /api/v1/accounts/{id}/withdraw`

### DI registrace

```csharp
services.AddPaymentsInfrastructure(connectionString, accountsApiUrl);
// → EF Core DbContext + 3 repozitáře + HttpClient pro Accounts
```

---

## API Endpointy

Všechny endpointy jsou na payments-api (port 8080, Development: 8003).

### Platby

| Metoda | Route | Popis |
|--------|-------|-------|
| `POST` | `/api/v1/payments` | Odeslat platbu |
| `GET` | `/api/v1/payments/account/{accountId}` | Historie plateb účtu |

### Trvalé příkazy

| Metoda | Route | Popis |
|--------|-------|-------|
| `POST` | `/api/v1/standing-orders` | Vytvořit trvalý příkaz |
| `GET` | `/api/v1/standing-orders/account/{accountId}` | Seznam příkazů účtu |
| `DELETE` | `/api/v1/standing-orders/{id}` | Zrušit trvalý příkaz |

### Šablony

| Metoda | Route | Popis |
|--------|-------|-------|
| `POST` | `/api/v1/payment-templates` | Vytvořit šablonu |
| `GET` | `/api/v1/payment-templates/account/{accountId}` | Seznam šablon účtu |
| `DELETE` | `/api/v1/payment-templates/{id}` | Smazat šablonu |

### Health check

`GET /health` — vrací 200 OK

---

## YARP Gateway routing

Všechny platební endpointy jsou směrovány přes API Gateway:

| Route | Pattern | Cluster |
|-------|---------|---------|
| `payments-route` | `/api/v1/payments/{**catch-all}` | `payments-cluster` |
| `standing-orders-route` | `/api/v1/standing-orders/{**catch-all}` | `payments-cluster` |
| `payment-templates-route` | `/api/v1/payment-templates/{**catch-all}` | `payments-cluster` |
| `payments-health` | `/payments/health` | `payments-cluster` |

Cluster: `http://payments-api:8080` (Docker) / `http://localhost:8003` (Development)

---

## Frontend — `Payments.razor` (route: `/platby`)

Stránka s 5 záložkami (tab navigace):

### 1. Nová platba (tab: `payment`)

- Výběr ze šablony (dropdown, pokud existují)
- Číslo účtu příjemce, částka, popis
- Odesílá `SendPaymentAsync(isInstant: false)`

### 2. Okamžitá platba (tab: `instant`)

- Stejný formulář jako standardní platba
- Info banner: "⚡ Okamžitá platba bude zpracována ihned"
- Odesílá `SendPaymentAsync(isInstant: true)`

### 3. Převod mezi účty (tab: `transfer`)

- Dropdown "Z účtu" / "Na účet" (filtrovaný seznam vlastních účtů)
- Částka
- Odesílá standardní platbu — backend automaticky detekuje `InternalTransfer`

### 4. Trvalé příkazy (tab: `standing`)

- Formulář: příjemce, částka, popis, interval (denně/týdně/měsíčně/čtvrtletně/ročně), první platba, volitelný konec
- Seznam aktivních příkazů s tlačítkem "Zrušit"

### 5. Šablony (tab: `templates`)

- Formulář: název, příjemce, jméno příjemce, výchozí částka, výchozí popis
- Seznam uložených šablon s tlačítky "Použít" (přepne na tab platby a vyplní formulář) a "Smazat"

### Společné prvky

- Historie plateb (spodní karta) — zobrazuje se na všech záložkách
- Success/error bannery po každé akci
- Paralelní načítání dat (`Task.WhenAll`)
- Automatický refresh po každé operaci

---

## Docker & Síťová izolace

### Nový service v `docker-compose.yml`

```yaml
payments-api:
  container_name: fairbank-payments-api
  expose: ["8080"]
  environment:
    ConnectionStrings__DefaultConnection: "...Search Path=payments_service"
    Services__AccountsApi: "http://accounts-api:8080"
  depends_on: [postgres-primary, accounts-api]
  networks: [backend]
```

### Síťová izolace (lockdown)

Dva oddělené Docker networky:

| Síť | Typ | Služby | Přístup zvenčí |
|-----|-----|--------|----------------|
| `frontend` | bridge | web-app, api-gateway | ✅ Ano (port 80) |
| `backend` | bridge, `internal: true` | api-gateway, identity-api, accounts-api, payments-api, postgres-primary, postgres-replica | ❌ Ne |

- **web-app** — jediná služba s `ports: "80:80"`, na `frontend` síti
- **api-gateway** — most mezi oběma sítěmi (`frontend` + `backend`)
- Všechny API + DB — pouze `backend` (interní síť, bez přístupu z hostu)

### PostgreSQL schema

```sql
CREATE SCHEMA IF NOT EXISTS payments_service;
GRANT ALL PRIVILEGES ON SCHEMA payments_service TO fairbank_app;
```

---

## Rozšíření existujících služeb

### Accounts Service — nový endpoint

`GET /api/v1/accounts/by-number/{accountNumber}` — vyhledání účtu podle čísla (FAIR-XXXX).

Přidáno:
- `GetAccountByNumberQuery` + handler v Application
- `LoadByAccountNumberAsync` v `IAccountEventStore` / `MartenAccountEventStore`
- Endpoint v `AccountEndpoints.cs`

### Web.Shared — nové modely a API metody

Nové DTO: `PaymentDto`, `StandingOrderDto`, `PaymentTemplateDto`

Nové metody v `IFairBankApi` / `FairBankApiClient`:
- `SendPaymentAsync`, `GetPaymentsByAccountAsync`
- `CreateStandingOrderAsync`, `GetStandingOrdersByAccountAsync`, `CancelStandingOrderAsync`
- `CreatePaymentTemplateAsync`, `GetPaymentTemplatesByAccountAsync`, `DeletePaymentTemplateAsync`

---

## Jak spustit

```bash
# Celý stack v Dockeru
docker-compose up --build

# Development (lokálně)
dotnet run --project src/Services/Payments/FairBank.Payments.Api
# → http://localhost:8003
# → Scalar docs: http://localhost:8003/scalar/v1
```

## Jak otestovat

```bash
# Build celého řešení
dotnet build

# Spuštění testů
dotnet test
```
