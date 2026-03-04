# Design: Oprava funkcí aktéra "Dítě"

**Datum:** 2026-03-04
**Stav:** Schváleno

## Kontext

Analýza odhalila, že ze 7 požadovaných funkcí aktéra "Dítě" je plně funkční pouze chat (rodič–dítě). Zbývající funkce buď chybí, nebo existují izolovaně na backendu bez integrace a frontendu.

## Scope

### Opravujeme:
1. Integrace spending limitů do flow plateb
2. Frontend správa dětských účtů + schvalování plateb rodičem
3. In-app notifikace rodiče o transakcích dítěte
4. Propojení schvalování PendingTransaction s provedením platby

### Nepravíme (přeskočeno):
- Edukační modul
- Systém odměn

---

## 1. Integrace limitů do plateb

### Problém
`SendPaymentCommandHandler` v Payments service nekontroluje spending limity nastavené na účtu dítěte. Platba projde bez ohledu na limit. `PendingTransaction` aggregate existuje, ale není propojen s flow plateb.

### Řešení

**Rozšíření `IAccountsService`** (Payments.Application):
- `GetAccountLimitsAsync(Guid accountId)` → vrátí `SpendingLimitDto { SpendingLimit, RequiresApproval, ApprovalThreshold }`
- `CreatePendingTransactionAsync(...)` → vytvoří PendingTransaction v Accounts service

**Nový endpoint v Accounts API:**
- `GET /api/v1/accounts/{id}/limits` → vrátí limit info

**Úprava `SendPaymentCommandHandler.Handle()`:**
```
1. Načti sender account
2. Načti limity přes GetAccountLimitsAsync()
3. IF requiresApproval AND amount > approvalThreshold:
   a. Vytvoř PendingTransaction přes Accounts API
   b. Vytvoř Payment se statusem PendingApproval
   c. Vytvoř notifikaci pro rodiče
   d. Vrať odpověď "čeká na schválení"
4. ELSE: proveď platbu normálně
```

**Nový PaymentStatus:** `PendingApproval` v enumu `PaymentStatus`

---

## 2. Frontend správa dětských účtů

### Pro rodiče (role Client):

**Stránka `/deti`** — nová položka v SideNav (viditelná jen pro Client):
- Seznam dětí s účty, zůstatky, limity
- Tlačítko "Přidat dítě" → formulář (jméno, příjmení, email, heslo)
- Po vytvoření dítěte automaticky: vytvořit User + Account + Family chat konverzaci

**Stránka `/deti/{childId}`** — detail dítěte:
- Přehled účtu (zůstatek, poslední transakce)
- Nastavení spending limitu + approval thresholdu
- Panel čekajících plateb s tlačítky Schválit / Zamítnout

### Pro dítě (role Child):

**Overview stránka** — přizpůsobit:
- Info o limitu ("Tvůj denní limit: 500 CZK")
- Stav čekajících plateb

### SideNav:
- Client: přidat "Děti" (ikona users)
- Child: skrýt Produkty, Spoření, Investice — ponechat Overview, Platby, Zprávy

---

## 3. In-app notifikace (v Identity service)

### Proč Identity service:
- Už spravuje uživatele a vztahy rodič-dítě
- Používá EF Core + PostgreSQL
- Žádný nový Docker container potřeba

### Doménový model:

**Notification** (Entity):
- `Id` (Guid)
- `UserId` (Guid) — příjemce
- `Type` (enum): TransactionCompleted, TransactionPending, TransactionApproved, TransactionRejected
- `Title` (string)
- `Message` (string)
- `IsRead` (bool)
- `RelatedEntityId` (Guid?)
- `RelatedEntityType` (string?)
- `CreatedAt` (DateTime)

### DB:
- Tabulka `notifications` ve schématu `identity_service`

### API endpointy:
- `GET /api/v1/notifications?userId={guid}&unreadOnly=true`
- `POST /api/v1/notifications/{id}/read`
- `POST /api/v1/notifications/read-all?userId={guid}`
- `POST /api/v1/notifications` — interní, volají ostatní služby
- `GET /api/v1/notifications/count?userId={guid}`

### YARP:
- Nová route `/api/v1/notifications/**` → `identity-api:8080`

### Frontend:
- Funkční zvoneček s počítadlem (polling 30s)
- Dropdown s posledními notifikacemi
- Stránka `/notifikace` s kompletním seznamem

### Integrace:
- Payments service po platbě → HTTP POST na Identity API pro vytvoření notifikace
- Accounts service po PendingTransaction → totéž

---

## 4. Propojení schvalování plateb

### Flow:
1. Dítě provede platbu nad threshold
2. Payments vytvoří PendingTransaction + Payment(PendingApproval)
3. Notifikace rodiči
4. Rodič na stránce `/deti/{childId}` vidí čekající platbu
5. Rodič schválí → Accounts provede Withdraw, Payments aktualizuje Payment na Completed, notifikace dítěti
6. Rodič zamítne → Payments aktualizuje Payment na Cancelled, notifikace dítěti s důvodem

### Technicky:
- `ApproveTransactionCommandHandler` po schválení zavolá Withdraw + aktualizuje linked Payment
- Potřeba propojit PendingTransaction s Payment ID (nové pole `LinkedPaymentId` na PendingTransaction nebo Payment)

---

## Infrastruktura

- Žádný nový Docker container
- Žádný nový port
- Nová YARP route pro `/api/v1/notifications/**`
- Nová DB tabulka `identity_service.notifications`
- Rozšíření `AccountsServiceHttpClient` v Payments service
- Nový `IdentityServiceHttpClient` v Payments a Accounts service (pro notifikace)
