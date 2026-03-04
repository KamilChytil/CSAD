# FairBank — Changelog session 2026-03-03

> Souhrnná dokumentace všech změn provedených během hackathonové session.

---

## 1. Přehled řešených problémů

| # | Problém | Příčina | Řešení |
|---|---------|---------|--------|
| 1 | 500 / 405 na API endpointech (Chat, Accounts) | Chybějící GET `/api/v1/accounts?ownerId=` endpoint; Chat DB oprávnění v `init.sql` | Přidán endpoint; opravena práva v init.sql |
| 2 | Stejné číslo účtu u všech uživatelů | Seeder nepředával deterministic account number do `CreateAccountCommand` | Přidán `AccountNumber` parametr do command → handler → domain |
| 3 | Zůstatek 0 u všech uživatelů | Marten inline snapshot neserializoval `private set` properties (STJ) | Přidáno `[JsonConstructor]` a `[JsonInclude]` na všechny entity |
| 4 | Frontend nezobrazoval žádné účty | `Currency` enum se serializoval jako `int` (0), frontend čekal `string` ("CZK") | Přidán `JsonStringEnumConverter` do Accounts API `Program.cs` |
| 5 | Platba se nezdařila (POST /payments → 500) | Tabulky `payments_service.payments` neexistovaly; `EnsureCreatedAsync()` nefunguje pro existující DB | Nahrazeno cílenou kontrolou + `GenerateCreateScript()` |
| 6 | Platba odešla, ale příjemci nedorazila | `/` v čísle účtu (`000000-xxx/8888`) se URL-enkódoval na `%2F` → ASP.NET Core routing → 404 | Endpoint přesunut z route parametru na query parametr |
| 7 | Emoji ikony na Login/Register stránkách | UI design požadavek | Nahrazeny SVG ikonami přes `VbIcon` komponentu |
| 8 | Duplicitní „Platby" a „Okamžitá platba" | UI design — oboje dělalo to samé | Odstraněno „Okamžitá", nahrazeno záložkou „Zahraniční" |

---

## 2. Architektura platebního systému

### Tok platby (Nová platba)

```
[Frontend: Payments.razor]
        │
        ▼  POST /api/v1/payments
[API Gateway (YARP)]
        │
        ▼
[Payments API]
  ├─ 1. GET /api/v1/accounts/{senderId}  →  ověří sender existuje & aktivní
  ├─ 2. GET /api/v1/accounts/by-number?accountNumber=...  →  najde recipient
  ├─ 3. Kontrola zůstatku (balance ≥ amount)
  ├─ 4. Vytvoří Payment záznam (EF Core, payments_service.payments)
  ├─ 5. POST /api/v1/accounts/{senderId}/withdraw  →  odečte z odesílatele
  ├─ 6. POST /api/v1/accounts/{recipientId}/deposit  →  přičte příjemci
  └─ 7. Označí Payment jako Completed
```

### Tok historie plateb (Pohyby)

```
[Frontend: Payments.razor — záložka "Pohyby"]
        │
        ▼  GET /api/v1/payments/account/{accountId}
[Payments API]
        │
        ▼  EF Core query: WHERE SenderAccountId = @id OR RecipientAccountId = @id
[payments_service.payments tabulka]
```

Frontend rozlišuje odeslané (→, červeně) a přijaté (←, zeleně) podle `SenderAccountId == currentAccountId`.

---

## 3. Formát čísla účtu

### Starý formát (odstraněn)
```
FAIR-XXXX-XXXX-XXXX     (např. FAIR-1000-0000-0001)
```

### Nový formát (český standard)
```
předčíslí-číslo_účtu/kód_banky     (např. 000000-1000000001/8888)
```

- **Předčíslí**: 6 číslic (volitelné, default `000000`)
- **Číslo účtu**: 2–10 číslic
- **Kód banky**: 4 číslice, FAIRBank = `8888`

### Seedovaní uživatelé

| Role | E-mail | Heslo | Číslo účtu | Zůstatek |
|------|--------|-------|------------|----------|
| Admin | admin@fairbank.cz | Admin123! | 000000-1000000001/8888 | 100 000 Kč |
| Bankéř | banker@fairbank.cz | Banker123! | 000000-2000000002/8888 | 50 000 Kč |
| Klient | client@fairbank.cz | Client123! | 000000-3000000003/8888 | 10 000 Kč |

---

## 4. Modifikované soubory

### Backend — Accounts Service

| Soubor | Změna |
|--------|-------|
| `AccountNumber.cs` | Generování českého formátu; konstanta `FairBankCode = "8888"` |
| `Account.cs` | `[JsonConstructor]`, `[JsonInclude]` pro STJ/Marten |
| `Money.cs` | `[JsonConstructor]` |
| `PendingTransaction.cs` | `[JsonConstructor]`, `[JsonInclude]` |
| `CreateAccountCommand.cs` | Přidán `string? AccountNumber` parametr |
| `CreateAccountCommandHandler.cs` | Předání `AccountNumber` do `Account.Create()` |
| `AccountSeeder.cs` | Deterministická čísla účtů v CZ formátu |
| `AccountEndpoints.cs` | `/by-number/{accountNumber}` → `/by-number?accountNumber=` (query param) |
| `Program.cs` (Accounts API) | `JsonStringEnumConverter` pro enum → string serializaci |

### Backend — Payments Service

| Soubor | Změna |
|--------|-------|
| `Program.cs` (Payments API) | `EnsureCreatedAsync()` → cílená kontrola + `GenerateCreateScript()` |
| `AccountsServiceHttpClient.cs` | URL z route param na query param pro by-number |

### Frontend — Blazor WASM

| Soubor | Změna |
|--------|-------|
| `Payments.razor` | Kompletní přepracování: split account number (předčíslí/číslo/banka dropdown), zahraniční platby (IBAN/SWIFT), filtry historie, sumární statistiky |
| `Login.razor` | Emoji → SVG ikony (VbIcon) |
| `Register.razor` | Emoji → SVG ikony (VbIcon) |
| `Overview.razor` | Default balance `0m`, account number `"---"` |
| `Profile.razor` | Default account number `"---"` |
| `VbIcon.razor` | Přidány ikony: `eye`, `eye-off`, `lock`, `danger`, `check` |
| `AccountResponse.cs` | Dočasně `int Currency` → vráceno na `string Currency` |
| `vabank-theme.css` | CSS pro `.account-number-row`, `.field-error`, `.currency-select` |

### Infrastruktura

| Soubor | Změna |
|--------|-------|
| `init.sql` | Opravena DB oprávnění pro chat_service schema |

---

## 5. Platební stránka — UI struktura

### Záložky hlavní navigace
- **PLATBY** — Quick actions grid, kontakty, kalendář plateb
- **Pohyby** — Historie transakcí s filtrací (Vše / Odeslané / Přijaté) + sumární statistiky
- **Menu** — Formuláře pro jednotlivé operace

### Menu — dostupné operace

| Tab | Popis | Stav |
|-----|-------|------|
| Nová platba | Předčíslí-číslo/banka dropdown, částka, popis | ✅ Funkční |
| Převod | Mezi vlastními účty (dropdown výběr) | ✅ Funkční |
| Zahraniční | IBAN, SWIFT/BIC, měna (EUR/USD/GBP/CZK/CHF/PLN), typ poplatku (SHA/OUR/BEN) | ✅ UI hotové |
| Trvalé příkazy | Interval (denně–ročně), datum od/do | ✅ Funkční |
| Šablony | Uložení a použití oblíbených plateb | ✅ Funkční |

### Validace vstupů
- Číslo účtu: pouze číslice, předčíslí max 6, číslo 2–10 číslic
- IBAN: 15–34 znaků, regex `^[A-Z]{2}\d{2}[A-Z0-9]+$`
- SWIFT/BIC: 8 nebo 11 znaků, regex `^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$`
- Částka: musí být kladná

### České bankovní kódy v dropdown menu
| Kód | Banka |
|-----|-------|
| 0100 | Komerční banka |
| 0300 | ČSOB |
| 0600 | Moneta Money Bank |
| 0710 | ČNB |
| 0800 | Česká spořitelna |
| 2010 | Fio banka |
| 2700 | UniCredit Bank |
| 3030 | Air Bank |
| 3500 | ING Bank |
| 5500 | Raiffeisenbank |
| 5800 | J&T Banka |
| 6000 | PPF banka |
| 6100 | Equa bank |
| 6200 | CREDITAS |
| 6210 | mBank |
| 8040 | Oberbank |
| 8060 | Česká exportní banka |
| 8150 | HSBC |
| 8888 | FairBank |

---

## 6. Technické detaily — klíčové opravy

### 6.1 Marten + System.Text.Json

Marten ukládá inline snapshoty jako JSON v PostgreSQL. STJ (System.Text.Json) je default serializer. Problém: STJ nedokáže deserializovat entity s `private set` a bez bezparametrového konstruktoru.

**Řešení:**
```csharp
// Na entitách s private set properties:
[JsonInclude] public Money Balance { get; private set; }

// Na třídách bez public konstruktoru:
[JsonConstructor] private Account() { }
```

### 6.2 Enum serializace

ASP.NET Core Minimal API serializuje enum hodnoty jako čísla (`Currency.CZK` → `0`). Frontend a inter-service komunikace očekávají string.

**Řešení** (Accounts API `Program.cs`):
```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

### 6.3 URL routing s `/` v účtovém čísle

Český formát čísla účtu obsahuje `/` (`000000-xxx/8888`). Při vložení do URL path se `%2F`-enkóduje, ale ASP.NET Core dekóduje `%2F` zpět na `/` v routing pipeline → 404.

**Řešení:** Endpoint přesunut z path parametru na query parametr:
```
PŘED: GET /api/v1/accounts/by-number/{accountNumber}
PO:   GET /api/v1/accounts/by-number?accountNumber=000000-xxx/8888
```

### 6.4 EnsureCreatedAsync() nefunguje se sdílenou DB

`EnsureCreatedAsync()` kontroluje pouze existenci celé databáze, ne jednotlivých tabulek/schémat. Protože `init.sql` vytváří DB + schémata, EF Core vidí existující DB a nic nevytvoří.

**Řešení:**
```csharp
// Cílená kontrola existence tabulky + generování DDL z EF modelu
var exists = (bool)(await checkCmd.ExecuteScalarAsync())!;
if (!exists)
{
    var script = db.Database.GenerateCreateScript();
    await createCmd.ExecuteNonQueryAsync();
}
```

---

## 7. Docker prostředí

### Služby

| Služba | Image | Port | Health |
|--------|-------|------|--------|
| postgres-primary | postgres:16-alpine | 5432 (internal) | ✅ |
| postgres-replica | postgres:16-alpine | 5433 (internal) | ✅ |
| kafka | apache/kafka:latest | 9092 (internal) | — |
| identity-api | hackathon-identity-api | 8080 (internal) | ✅ |
| accounts-api | hackathon-accounts-api | 8080 (internal) | ✅ |
| payments-api | hackathon-payments-api | 8080 (internal) | ✅ |
| chat-api | hackathon-chat-api | 8080 (internal) | ✅ |
| api-gateway | hackathon-api-gateway (YARP) | 8080 (internal) | ✅ |
| web-app | hackathon-web-app (Blazor WASM + Nginx) | **80 (external)** | ✅ |
| admin-web-app | hackathon-admin-web-app | **8081 (external)** | ✅ |

### Příkazy

```bash
# Start
docker compose up -d

# Rebuild vše
docker compose up --build -d

# Rebuild jen konkrétní service
docker compose up --build -d accounts-api payments-api web-app

# Reset DB (smaže volumes → fresh seed)
docker compose down -v
docker compose up --build -d

# Logy
docker compose logs payments-api --tail 50
docker compose logs accounts-api --tail 50

# SQL dotazy
docker compose exec postgres-primary psql -U fairbank_app -d fairbank -c "SELECT ..."
```

---

## 8. Jak testovat platby

1. Otevřít `http://localhost` v prohlížeči
2. Přihlásit se jako `admin@fairbank.cz` / `Admin123!`
3. Přejít na **Platby** → **Menu** → **Nová platba**
4. Zadat číslo účtu příjemce: předčíslí `000000`, číslo `3000000003`, banka `8888 — FairBank`
5. Zadat částku (např. 1000) a popis
6. Kliknout **Odeslat platbu**
7. Přepnout na záložku **Pohyby** — vidět odeslanou platbu (-1000)
8. Přihlásit se jako `client@fairbank.cz` / `Client123!`
9. Na **Pohyby** vidět příchozí platbu (+1000) a navýšený zůstatek
