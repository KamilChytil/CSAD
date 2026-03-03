# Zabezpečení přihlašování — Server-side lockout, Single-session, BCrypt

> **Datum:** 2026-03-03  
> **Služba:** Identity Service + FairBank.Web.Shared (AuthService)  
> **Cíl:** Všechny bezpečnostní kontroly musí být vynuceny na serveru (DB). Klient (localStorage, browser DevTools) NESMÍ být schopen obejít žádnou ochranu.

---

## Přehled změn

| # | Změna | Kde | Proč |
|---|-------|-----|------|
| 1 | **Server-side eskaltující lockout** | `User` entita + DB | Předchozí lockout byl v localStorage → obejití smazáním klíče |
| 2 | **Jedna aktivní relace (single-session)** | `User.ActiveSessionId` v DB | Přihlášení z 2. prohlížeče invaliduje předchozí session |
| 3 | **Server-enforced platnost session** | `User.SessionExpiresAt` v DB | `ExpiresAt` se dříve kontroloval jen na klientu → editace v DevTools obešla |
| 4 | **Oprava BCrypt** | Login handler + Admin seeder | Registrace hashovala heslo BCrypt, přihlášení ověřovalo SHA256 → login nikdy nefungoval |
| 5 | **Fail-closed validace** | `AuthService.ValidateSessionAsync` | Při nedostupnosti serveru se session považuje za neplatnou |
| 6 | **Removal lockoutu z localStorage** | `AuthService` | `fairbank_locked_until` klíč zcela odstraněn z localStorage |

---

## Princip: „Server je autorita"

```
┌─────────────────────────────────────────────────────────────┐
│                        KLIENT (Blazor WASM)                 │
│                                                             │
│  localStorage:                                              │
│    fairbank_session  →  { SessionId, Token, ExpiresAt, … }  │
│    fairbank_user     →  { jméno, role } (jen pro UI)        │
│                                                             │
│  ⚠️ Lockout se v localStorage NEUKLÁDÁ.                     │
│  ⚠️ Smazání localStorage = odhlášení (ne obejití).          │
│  ⚠️ Editace ExpiresAt v localStorage nemá efekt —           │
│     server kontroluje SessionExpiresAt v DB.                │
└──────────────────────────┬──────────────────────────────────┘
                           │  HTTP
┌──────────────────────────▼──────────────────────────────────┐
│                        SERVER (Identity API)                │
│                                                             │
│  PostgreSQL tabulka identity_service.users:                  │
│    FailedLoginAttempts   int       (počet špatných pokusů)  │
│    LockedUntil           timestamp (do kdy je účet zamčen)  │
│    ActiveSessionId       uuid      (právě platná session)   │
│    SessionExpiresAt      timestamp (platnost session - 8h)  │
│                                                             │
│  ✅ Lockout: kontrola v DB PŘED ověřením hesla              │
│  ✅ Session: kontrola ActiveSessionId + SessionExpiresAt    │
│  ✅ BCrypt.Verify pro heslo (work factor 12)                │
└─────────────────────────────────────────────────────────────┘
```

---

## 1. Eskalující lockout (server-side)

### Mechanismus

Lockout se počítá a vynucuje **výhradně v DB** na entitě `User`:

| Neúspěšných pokusů | Doba blokace | Uloženo v |
|---------------------|-------------|-----------|
| 1–4 | žádná | `User.FailedLoginAttempts` (DB) |
| 5–7 | **10 minut** | `User.LockedUntil` (DB) |
| 8–11 | **60 minut** | `User.LockedUntil` (DB) |
| 12+ | **24 hodin** | `User.LockedUntil` (DB) |

### Doménová metoda (`User.cs`)

```csharp
public void RecordFailedLogin()
{
    FailedLoginAttempts++;
    UpdatedAt = DateTime.UtcNow;

    LockedUntil = FailedLoginAttempts switch
    {
        >= 12 => DateTime.UtcNow.AddHours(24),
        >= 8  => DateTime.UtcNow.AddHours(1),
        >= 5  => DateTime.UtcNow.AddMinutes(10),
        _     => null
    };
}
```

### Login handler flow

```
POST /api/v1/users/login (email, password)
       │
       ├── User nenalezen nebo neaktivní → 401
       │
       ├── User.IsLockedOut == true → throw UserLockedOutException
       │       → endpoint vrátí HTTP 429 + { IsLockedOut, LockedUntil, RemainingSeconds }
       │
       ├── BCrypt.Verify(password, hash) == false
       │       → user.RecordFailedLogin()
       │       → unitOfWork.SaveChangesAsync()  // zapsáno do DB
       │       → pokud právě překročen práh → throw UserLockedOutException (429)
       │       → jinak → 401
       │
       └── Heslo správné
               → user.RecordSuccessfulLogin(sessionId, expiresAt)
               → unitOfWork.SaveChangesAsync()  // reset counters v DB
               → vrátí 200 + LoginResponse(Token, SessionId, ExpiresAt, …)
```

### Co se stane, když uživatel smaže localStorage?

1. Uživatel otevře DevTools → localStorage → smaže `fairbank_locked_until` → **klíč NEEXISTUJE** (odstraněn v této verzi)
2. Uživatel smaže `fairbank_session` → je odhlášen (nemá token) → musí se přihlásit znovu
3. Přihlášení → server pošle request → server zkontroluje `User.LockedUntil` v DB → **vrátí 429**
4. **Lockout NELZE obejít smazáním čehokoliv v prohlížeči.**

---

## 2. Jedna aktivní relace (single-session enforcement)

### Mechanismus

Každé úspěšné přihlášení:
1. Vygeneruje nový `SessionId` (random `Guid`)
2. Zapíše ho do `User.ActiveSessionId` v DB
3. Předchozí session je tím automaticky invalidována (přepsáním)

### Validace session

```
GET /api/v1/users/session/validate
    Authorization: Bearer <Base64(userId:sessionId)>
       │
       ├── Token nelze dekódovat → 401
       ├── User nenalezen nebo neaktivní → 401
       ├── user.IsSessionValid(sessionId) == false → 401
       │       (sessionId neodpovídá ActiveSessionId NEBO SessionExpiresAt vypršel)
       └── ✅ 200 { valid: true }
```

### Kdy se validace volá?

| Okamžik | Metoda | Chování při neúspěchu |
|---------|--------|----------------------|
| **Start aplikace** (každý page load) | `AuthService.InitializeAsync()` | `ClearSessionAsync()` → odhlášení |
| **Manuální volání** | `AuthService.ValidateSessionAsync()` | Vrátí `false` |

### Fail-closed

Pokud server není dostupný (síťová chyba), `ValidateSessionAsync()` vrátí `false` → uživatel je odhlášen. Bankovní aplikace nesmí povolit přístup bez ověření session.

```csharp
catch
{
    // Network error — fail closed. Banking app must not allow access
    // when it cannot verify the session with the server.
    return false;
}
```

### Flow přihlášení ze 2 prohlížečů

```
Čas    Prohlížeč A                  Server (DB)                   Prohlížeč B
────   ───────────                  ───────────                   ───────────
T+0    Login → SessionId=aaa        ActiveSessionId=aaa           (nepřihlášen)
T+5    (surfuje)                                                  Login → SessionId=bbb
                                    ActiveSessionId=bbb ← přepsáno!
T+6    Refresh stránky
       → InitializeAsync()
       → ValidateSessionAsync()
       → server: aaa ≠ bbb → 401
       → ClearSessionAsync()
       → ODHLÁŠEN ✅                                              (přihlášen)
```

---

## 3. Server-enforced platnost session (SessionExpiresAt)

### Problém (před opravou)

`ExpiresAt` se posílal klientovi v `LoginResponse` a ukládal do `AuthSession` v localStorage.  
Kontrola probíhala **jen na klientu**: `_currentSession.ExpiresAt <= DateTime.UtcNow`.  
→ Uživatel mohl v DevTools editovat `ExpiresAt` na rok 2099 a session fungovala neomezeně.

### Řešení

`SessionExpiresAt` je nyní uložen i v DB na entitě `User`:

```csharp
public void RecordSuccessfulLogin(Guid sessionId, DateTime expiresAtUtc)
{
    FailedLoginAttempts = 0;
    LockedUntil = null;
    ActiveSessionId = sessionId;
    SessionExpiresAt = expiresAtUtc;  // ← uloženo v DB
    UpdatedAt = DateTime.UtcNow;
}

public bool IsSessionValid(Guid sessionId)
    => ActiveSessionId.HasValue
       && ActiveSessionId.Value == sessionId
       && SessionExpiresAt.HasValue
       && SessionExpiresAt.Value > DateTime.UtcNow;  // ← server kontroluje platnost
```

| Kontrola | Kde | Obejitelné? |
|----------|-----|-------------|
| `_currentSession.ExpiresAt` | Klient (in-memory) | Ano (ale jen vizuálně) |
| `User.SessionExpiresAt` | Server (DB) | **Ne** — validace v `IsSessionValid()` |

---

## 4. Oprava BCrypt (kritický bug)

### Před opravou

| Operace | Hash algoritmus | Výsledek |
|---------|----------------|----------|
| Registrace (`RegisterUserCommandHandler`) | BCrypt | Hash: `$2a$12$...` |
| Přihlášení (`LoginUserCommandHandler`) | SHA256 | Porovnání: `SHA256(heslo) ≠ $2a$12$...` |
| Admin seeder (`AdminSeeder`) | SHA256 | Hash: `base64(SHA256(...))` |

**→ Login NIKDY nefungoval pro reálné uživatele.** SHA256 hash nikdy neodpovídal BCrypt hashi.

### Po opravě

| Operace | Hash algoritmus |
|---------|----------------|
| Registrace | `BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12)` |
| Přihlášení | `BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)` |
| Admin seeder | `BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12)` |

---

## 5. localStorage — co tam zůstává a co ne

### Stav po hardening

| Klíč | Přítomen? | Účel | Smazání = obejití? |
|------|-----------|------|-------------------|
| `fairbank_session` | ✅ Ano | Uchovává Token pro identifikaci uživatele vůči serveru | **Ne** — smazání = odhlášení (ztráta tokenu) |
| `fairbank_user` | ✅ Ano | Jméno + role pro UI zobrazení | **Ne** — role se nepoužívá pro autorizaci na serveru |
| `fairbank_locked_until` | ❌ **Odstraněn** | ~~Čas lockoutu~~ | N/A — lockout je pouze v DB + in-memory |
| `fairbank_login_attempts` | ❌ **Odstraněn** (dříve) | ~~Počet pokusů~~ | N/A — počítadlo je pouze v DB |

### Proč token v localStorage?

Blazor WebAssembly běží **celý v prohlížeči** (WASM). Nemá server-side session (jako ASP.NET MVC).  
Token v localStorage je **standardní pattern pro SPA** (React, Angular, Vue, Blazor WASM).  
Na serveru se token validuje při každém volání `ValidateSessionAsync()`.

---

## 6. Automatický timeout (neaktivita)

| Parametr | Hodnota |
|----------|---------|
| `InactivityTimeoutMinutes` | 5 minut |

### Mechanismus

- Po přihlášení se spustí `System.Threading.Timer`
- Při jakékoliv aktivitě uživatele → `ResetInactivityTimer()` (restart 5 min)
- Po 5 minutách bez aktivity:
  1. Timer fired → `OnInactivityTimeoutAsync()`
  2. `ClearSessionAsync()` — smaže session z localStorage
  3. `AuthStateChanged?.Invoke()` — UI se přepne na login
- Login stránka detekuje `?expired` query parameter → zobrazí upozornění

---

## API Endpointy (bezpečnostní)

| Metoda | Route | Popis | HTTP kódy |
|--------|-------|-------|-----------|
| `POST` | `/api/v1/users/login` | Přihlášení | 200 (OK), 401 (špatné heslo), 429 (lockout) |
| `POST` | `/api/v1/users/logout` | Odhlášení (invalidace session) | 204 (OK), 401 (neplatný token) |
| `GET` | `/api/v1/users/session/validate` | Ověření platnosti session | 200 (platná), 401 (neplatná/expir.) |

### Token formát

```
Bearer <Base64(UTF8("{userId}:{sessionId}"))>
```

Dekódování v `SessionTokenHelper.TryDecode()`:
```csharp
public static bool TryDecode(string token, out Guid userId, out Guid sessionId)
{
    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
    var parts = decoded.Split(':');
    return Guid.TryParse(parts[0], out userId) && Guid.TryParse(parts[1], out sessionId);
}
```

> **Pozn.:** Token není podepsaný JWT. Bezpečnost zajišťuje `ActiveSessionId` v DB — i kdyby útočník uhodl formát, potřebuje znát `sessionId` (128-bit random GUID).

---

## YARP routing (API Gateway)

Frontend volá `/api/v1/auth/*`, YARP transformuje na `/api/v1/users/*`:

```json
"auth-route": {
    "ClusterId": "identity-cluster",
    "Match": { "Path": "/api/v1/auth/{**catch-all}" },
    "Transforms": [
        { "PathPattern": "/api/v1/users/{**catch-all}" }
    ]
}
```

→ `POST /api/v1/auth/login` → identity-api obdrží `POST /api/v1/users/login`  
→ `POST /api/v1/auth/logout` → identity-api obdrží `POST /api/v1/users/logout`  
→ `GET /api/v1/auth/session/validate` → identity-api obdrží `GET /api/v1/users/session/validate`

---

## Databázové schéma (nové sloupce)

Tabulka `identity_service.users` — přidané sloupce:

| Sloupec | Typ | Default | Popis |
|---------|-----|---------|-------|
| `FailedLoginAttempts` | `int` | `0` | Počet po sobě jdoucích neúspěšných pokusů |
| `LockedUntil` | `timestamp` | `NULL` | Čas, do kdy je účet zamčen |
| `ActiveSessionId` | `uuid` | `NULL` | ID právě aktivní session |
| `SessionExpiresAt` | `timestamp` | `NULL` | Server-enforced platnost session (8 h) |

EF migrace: `AddLoginSecurity` + `AddSessionExpiresAt`  
Automaticky aplikovány při startu API: `db.Database.MigrateAsync()`

---

## Bezpečnostní testy — co ověřit

### ✅ Lockout nelze obejít

1. Zadat 5× špatné heslo → server vrátí 429 s `LockedUntil`
2. Otevřít DevTools → Application → localStorage → smazat vše
3. Zkusit login znovu → server STÁLE vrátí 429 (lockout je v DB)

### ✅ Single-session funguje

1. Přihlásit se v prohlížeči A
2. Přihlásit se v prohlížeči B (stejný účet)
3. V prohlížeči A refreshnout stránku → automaticky odhlášen

### ✅ Editace ExpiresAt nemá efekt

1. Přihlásit se
2. V DevTools → localStorage → `fairbank_session` → změnit `ExpiresAt` na rok 2099
3. Refreshnout stránku → `InitializeAsync()` → `ValidateSessionAsync()` volá server
4. Server kontroluje `SessionExpiresAt` v DB (původní 8h) → po 8h vrátí 401

### ✅ BCrypt funguje

1. Registrovat nového uživatele
2. Přihlásit se se správným heslem → úspěch (200)
3. Přihlásit se se špatným heslem → neúspěch (401)

### ✅ Inactivity timeout

1. Přihlásit se
2. Nechat 5 minut bez aktivity
3. → Automatické odhlášení + redirect na login s `?expired`

---

## Soubory změněné v rámci hardening

### Backend

| Soubor | Změna |
|--------|-------|
| `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs` | +`FailedLoginAttempts`, `LockedUntil`, `ActiveSessionId`, `SessionExpiresAt`, `IsLockedOut`, domain metody |
| `src/Services/Identity/FairBank.Identity.Domain/Entities/UserLockedOutException.cs` | **NOVÝ** — doménová výjimka s `LockedUntil` |
| `src/Services/Identity/FairBank.Identity.Application/Helpers/SessionTokenHelper.cs` | **NOVÝ** — `Encode`/`TryDecode` pro Base64 session token |
| `src/Services/Identity/FairBank.Identity.Application/Users/Commands/LoginUser/LoginUserCommandHandler.cs` | BCrypt.Verify, server-side lockout, `RecordFailedLogin`/`RecordSuccessfulLogin` |
| `src/Services/Identity/FairBank.Identity.Application/Users/Commands/LogoutUser/` | **NOVÝ** — `LogoutUserCommand` + handler (invalidace `ActiveSessionId`) |
| `src/Services/Identity/FairBank.Identity.Application/Users/Queries/ValidateSession/` | **NOVÝ** — `ValidateSessionQuery` + handler (kontrola `IsSessionValid`) |
| `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/LoginLockoutResponse.cs` | **NOVÝ** — DTO pro HTTP 429 odpověď |
| `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs` | Login→429 handling, `POST /logout`, `GET /session/validate` |
| `src/Services/Identity/FairBank.Identity.Api/Seeders/AdminSeeder.cs` | SHA256 → BCrypt |
| `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs` | Mapování 4 nových sloupců |
| `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Migrations/` | `AddLoginSecurity` + `AddSessionExpiresAt` |

### Frontend

| Soubor | Změna |
|--------|-------|
| `src/FairBank.Web.Shared/Services/AuthService.cs` | Odstraněn `fairbank_locked_until` z localStorage; lockout jen in-memory; fail-closed validace |
| `src/FairBank.Web.Shared/Models/LoginLockoutResponse.cs` | **NOVÝ** — model pro deserializaci 429 odpovědi |
