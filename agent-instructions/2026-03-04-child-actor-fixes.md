# Feature: Oprava funkcí aktéra "Dítě" (Child Actor Fixes)

> Date: 2026-03-04
> Design: `docs/plans/2026-03-04-child-actor-fixes-design.md`
> Plan: `docs/plans/2026-03-04-child-actor-fixes-plan.md`
> Commits: ~20 commits (24293a3 through c0ce76d)

---

## Context

Analýza odhalila, že ze 7 požadovaných funkcí aktéra "Dítě" fungoval plně pouze chat (rodič–dítě). Zbývajících 6 funkcí buď chybělo, nebo existovalo izolovaně na backendu bez integrace a frontendu.

**Opraveno (4 funkce):**
1. Integrace spending limitů do flow plateb
2. Frontend správa dětských účtů + schvalování plateb rodičem
3. In-app notifikace rodiče o transakcích dítěte
4. Propojení schvalování PendingTransaction s provedením platby

**Přeskočeno (2 funkce):**
- Edukační modul (statické lekce + kvízy) — záměrně odloženo
- Systém odměn (manuální odměny od rodiče) — záměrně odloženo

---

## 1. Integrace spending limitů do plateb

### Problém
`SendPaymentCommandHandler` v Payments service nekontroloval spending limity nastavené na účtu dítěte. Platba prošla bez ohledu na limit.

### Řešení

**Nový PaymentStatus:** `PendingApproval = 4` v enumu `PaymentStatus`
- Soubor: `src/Services/Payments/FairBank.Payments.Domain/Enums/PaymentStatus.cs`

**Nová metoda na Payment:** `MarkPendingApproval()`
- Soubor: `src/Services/Payments/FairBank.Payments.Domain/Entities/Payment.cs`

**Rozšíření IAccountsServiceClient** (Payments.Application):
- `GetSpendingLimitAsync(Guid accountId)` → vrátí `SpendingLimitInfo { RequiresApproval, ApprovalThreshold, Currency }`
- `CreatePendingTransactionAsync(...)` → vytvoří PendingTransaction v Accounts service
- `GetAccountLimitsAsync(Guid accountId)` → vrátí `AccountLimitsInfo`
- Soubor: `src/Services/Payments/FairBank.Payments.Application/Ports/IAccountsServiceClient.cs`

**HTTP implementace:**
- Soubor: `src/Services/Payments/FairBank.Payments.Infrastructure/HttpClients/AccountsServiceHttpClient.cs`

**Úprava SendPaymentCommandHandler.Handle():**
```
1. Načti sender account
2. Načti limity přes GetSpendingLimitAsync()
3. IF requiresApproval AND amount > approvalThreshold:
   a. Vytvoř PendingTransaction přes Accounts API
   b. Vytvoř Payment se statusem PendingApproval
   c. Vytvoř notifikaci pro rodiče
   d. Vrať odpověď "čeká na schválení"
4. ELSE: proveď platbu normálně
```
- Soubor: `src/Services/Payments/FairBank.Payments.Application/Payments/Commands/SendPayment/SendPaymentCommandHandler.cs`

**Nové Accounts API endpointy:**
- `GET /api/v1/accounts/{id}/limits` → vrátí limit info
- `POST /api/v1/accounts/pending` → vytvoří PendingTransaction externě
- Soubor: `src/Services/Accounts/FairBank.Accounts.Api/Endpoints/AccountEndpoints.cs`

**Nový command v Accounts:**
- `CreatePendingTransactionCommand` + handler
- Soubor: `src/Services/Accounts/FairBank.Accounts.Application/Commands/CreatePendingTransaction/`

**AccountResponse rozšířen** o spending limit pole (optional, defaults):
- `RequiresApproval`, `ApprovalThreshold`, `SpendingLimit`
- Soubor: `src/Services/Accounts/FairBank.Accounts.Application/DTOs/AccountResponse.cs`

**Testy:**
- 3 unit testy: exceeds threshold → PendingApproval, below threshold → Completed, no limit → Completed
- Soubor: `tests/FairBank.Payments.UnitTests/Application/SendPaymentWithLimitsTests.cs`

---

## 2. Frontend správa dětských účtů

### Pro rodiče (role Client):

**SideNav** — nová položka "Děti" (viditelná jen pro Client):
- Soubor: `src/FairBank.Web/Layout/SideNav.razor`

**Stránka `/deti`** (Children.razor):
- Seznam dětí s účty, zůstatky, limity
- Tlačítko "Přidat dítě" → modální formulář (jméno, příjmení, email, heslo)
- Po vytvoření dítěte automaticky: vytvořit User + Account + Family chat konverzaci
- Soubor: `src/FairBank.Web/Pages/Children.razor`

**Stránka `/deti/{childId}`** (ChildDetail.razor):
- Přehled účtu (zůstatek, poslední transakce)
- Nastavení spending limitu + approval thresholdu
- Panel čekajících plateb s tlačítky Schválit / Zamítnout
- Modál pro zamítnutí s důvodem
- Soubor: `src/FairBank.Web/Pages/ChildDetail.razor`

### Pro dítě (role Child):

**Overview stránka** — přizpůsobena:
- Info o limitu ("Tvůj denní limit: X CZK")
- Stav čekajících plateb
- Soubor: `src/FairBank.Web.Overview/Pages/Overview.razor`

**SideNav** — Child nemá viditelné: Spoření, Investice, Kurzy, Produkty

### API rozšíření:
- `GetNotificationsAsync`, `GetUnreadNotificationCountAsync`, `MarkNotificationReadAsync`, `MarkAllNotificationsReadAsync`
- `SetSpendingLimitAsync`, `GetOrCreateFamilyChatAsync`
- Soubor: `src/FairBank.Web.Shared/Services/IFairBankApi.cs` a `FairBankApiClient.cs`

### Nové DTOs:
- `NotificationDto` — `src/FairBank.Web.Shared/Models/NotificationDto.cs`
- `UserResponse` rozšířen o `Guid? ParentId`
- `AccountResponse` rozšířen o `RequiresApproval`, `ApprovalThreshold`, `SpendingLimit`

---

## 3. In-app notifikace (v Identity service)

### Proč Identity service:
- Už spravuje uživatele a vztahy rodič-dítě
- Používá EF Core + PostgreSQL
- Žádný nový Docker container

### Doménový model:

**Notification** (Entity):
- `Id`, `UserId`, `Type` (enum → string), `Title`, `Message`, `IsRead`, `RelatedEntityId`, `RelatedEntityType`, `CreatedAt`
- Factory metoda `Create()`, metoda `MarkAsRead()`
- Soubor: `src/Services/Identity/FairBank.Identity.Domain/Entities/Notification.cs`

**NotificationType** (Enum):
- TransactionCompleted, TransactionPending, TransactionApproved, TransactionRejected
- Soubor: `src/Services/Identity/FairBank.Identity.Domain/Enums/NotificationType.cs`

### Repository:
- `INotificationRepository`: GetByUserIdAsync, GetUnreadCountAsync, GetByIdAsync, AddAsync, UpdateAsync, MarkAllReadAsync
- Implementace s `ExecuteUpdateAsync` pro bulk MarkAllRead
- Soubory: `src/Services/Identity/FairBank.Identity.Domain/Ports/INotificationRepository.cs`, `...Infrastructure/Persistence/Repositories/NotificationRepository.cs`

### EF Core konfigurace:
- Tabulka `notifications` ve schématu `identity_service`
- Kompozitní index na (UserId, IsRead), index na CreatedAt
- Soubor: `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`

### CQRS stack:
- Commands: `CreateNotificationCommand`, `MarkNotificationReadCommand`, `MarkAllReadCommand`
- Queries: `GetNotificationsQuery`, `GetUnreadCountQuery`
- Soubor: `src/Services/Identity/FairBank.Identity.Application/Notifications/`

### API endpointy:
- `GET /api/v1/notifications?userId={guid}&unreadOnly=true`
- `GET /api/v1/notifications/count?userId={guid}`
- `POST /api/v1/notifications` — interní, volají ostatní služby
- `POST /api/v1/notifications/{id}/read`
- `POST /api/v1/notifications/read-all?userId={guid}`
- Soubor: `src/Services/Identity/FairBank.Identity.Api/Endpoints/NotificationEndpoints.cs`

### YARP route:
- `/api/v1/notifications/{**catch-all}` → `identity-cluster`
- Soubor: `src/FairBank.ApiGateway/appsettings.json`

### Frontend zvoneček:
- Funkční bell icon s počítadlem nepřečtených (polling 30s)
- Dropdown s posledními 10 notifikacemi
- Tlačítko "Označit vše jako přečtené"
- CSS využívá design system proměnné (--vb-red, --vb-bg-elevated, atd.)
- Soubor: `src/FairBank.Web/Layout/MainLayout.razor`

### Integrace s ostatními službami:
- **INotificationClient** interface + **NotificationHttpClient** implementace — fire-and-forget vzor
- **IIdentityClient** interface + **IdentityHttpClient** — pro lookup ParentId dítěte
- Registrováno v Payments i Accounts service

Soubory:
- `src/Services/Payments/FairBank.Payments.Application/Ports/INotificationClient.cs`
- `src/Services/Payments/FairBank.Payments.Application/Ports/IIdentityClient.cs`
- `src/Services/Payments/FairBank.Payments.Infrastructure/HttpClients/NotificationHttpClient.cs`
- `src/Services/Payments/FairBank.Payments.Infrastructure/HttpClients/IdentityHttpClient.cs`
- `src/Services/Accounts/FairBank.Accounts.Application/Ports/INotificationClient.cs`
- `src/Services/Accounts/FairBank.Accounts.Infrastructure/HttpClients/NotificationHttpClient.cs`

### Testy:
- 3 unit testy: create unread, mark as read, create with related entity
- Soubor: `tests/FairBank.Identity.UnitTests/Domain/NotificationTests.cs`

---

## 4. Propojení schvalování plateb

### Flow:
1. Dítě provede platbu nad threshold
2. Payments vytvoří PendingTransaction + Payment(PendingApproval)
3. Notifikace rodiči ("TransactionPending")
4. Rodič na stránce `/deti/{childId}` vidí čekající platbu
5. Rodič schválí → Accounts provede Withdraw, notifikace dítěti ("TransactionApproved")
6. Rodič zamítne → Payments aktualizuje Payment na Cancelled, notifikace dítěti ("TransactionRejected") s důvodem

### Úpravy:
- `ApproveTransactionCommandHandler` — přidán `INotificationClient`, po schválení posílá notifikaci dítěti
- `RejectTransactionCommandHandler` — přidán `INotificationClient`, po zamítnutí posílá notifikaci s důvodem
- Soubory: `src/Services/Accounts/FairBank.Accounts.Application/Commands/ApproveTransaction/`, `...RejectTransaction/`

---

## Infrastruktura a opravy

### docker-compose.yml:
- Přidáno `Services__IdentityApi: "http://identity-api:8080"` pro payments-api a accounts-api
- Přidáno identity-api do depends_on pro oba

### docker/postgres/init.sql:
- Přidány chybějící schémata `cards_service` a `notifications_service`
- Přidáno `GRANT CREATE ON DATABASE fairbank TO fairbank_app`
- Přidány granty pro cards_service a notifications_service

### Identity EF Core migration fix:
- Přidána migrace `20260304050000_AddTwoFactorAuthAndUserDevices` pro chybějící TwoFactorAuth a UserDevice tabulky
- Aktualizován model snapshot o obě entity
- Přidáno `ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))` v DependencyInjection.cs

### .dockerignore:
- Vytvořen pro redukci Docker build kontextu

### Directory.Packages.props:
- Přidány chybějící balíčky: `Microsoft.Extensions.Http`, `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`

---

## Klíčová architektonická rozhodnutí

1. **Notifikace v Identity service** (ne nový mikroservice) — uživatel explicitně požádal
2. **Fire-and-forget** vzor pro notifikace — selhání notifikace neblokuje operaci
3. **Polling 30s** pro zvoneček (ne SignalR/WebSocket) — jednodušší implementace
4. **Auto-provisioning** při vytvoření dítěte — automaticky account + family chat konverzace
5. **PendingApproval** payment status pro platby dítěte nad threshold

---

## Stav po implementaci

- Všech 13 Docker kontejnerů: **healthy**
- Identity API, Accounts API, Payments API, Cards API, Chat API, Products API, Notifications API, API Gateway, Web App, Admin Web, Kafka, PostgreSQL Primary, PostgreSQL Replica
- Všechny API endpointy funkční přes YARP gateway
