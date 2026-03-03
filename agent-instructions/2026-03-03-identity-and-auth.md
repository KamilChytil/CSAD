# Registrace a Autentizace — Popis implementace

> **Datum:** 2026-03-03  
> **Služba:** Identity Service (Agent 5) + FairBank.Web.Auth frontend

---

## Přehled

Implementace pokrývá kompletní flow registrace a přihlášení uživatelů v bankovní aplikaci FairBank. Systém zahrnuje:

1. **Registrace** — rozšířený formulář s mnoha osobními údaji
2. **Přihlášení** — autentizace emailem a heslem
3. **Limit přihlášení** — max 5 pokusů, poté dočasný zámek účtu
4. **Automatický timeout** — odhlášení po 5 minutách neaktivity
5. **Jedna aktivní relace** — nové přihlášení z jiného zařízení odhlásí předchozí session

---

## Architektura

Systém je postaven na **hexagonální architektuře** (Ports & Adapters) s oddělením vrstev:

```
FairBank.Identity.Domain            → Entity, Value Objects, Porty (rozhraní)
FairBank.Identity.Application       → CQRS příkazy/dotazy (MediatR), validace (FluentValidation)
FairBank.Identity.Infrastructure    → EF Core DbContext, repozitáře, migrace (PostgreSQL)
FairBank.Identity.Api               → Minimal API endpointy
FairBank.Web.Auth                   → Blazor WASM stránky (Login, Register)
FairBank.Web.Shared                 → Sdílené modely, AuthService, API klient
```

---

## 1. Registrace (rozšířený formulář)

### Frontend — `Register.razor` (route: `/registrace`)

Formulář je rozdělen do 3 sekcí:

| Sekce | Pole | Validace |
|-------|------|----------|
| **Osobní údaje** | Jméno (`_firstName`) | Povinné |
| | Příjmení (`_lastName`) | Povinné |
| | Rodné číslo (`_personalIdNumber`) | Povinné, min 9 číslic (bez `/`) |
| | Datum narození (`_dateOfBirth`) | Povinné, věk ≥ 15 let |
| | Telefon (`_phone`) | Povinné, min 9 číslic |
| **Adresa** | Ulice a číslo (`_street`) | Povinné |
| | Město (`_city`) | Povinné |
| | PSČ (`_zipCode`) | Povinné, min 5 znaků |
| | Země (`_country`) | Výběr: CZ, SK, DE, AT, PL (default: CZ) |
| **Přihlašovací údaje** | Email (`_email`) | Povinné, musí obsahovat `@` |
| | Heslo (`_password`) | Min 8 znaků, velké písmeno, číslice |
| | Potvrzení hesla (`_passwordConfirm`) | Musí se shodovat |
| | Souhlas s podmínkami (`_agreedToTerms`) | Musí být zaškrtnut |

**Indikátor síly hesla:** Vizuální progress bar 0–100 % (4 × 25 %):
- Délka ≥ 8 znaků
- Obsahuje velké písmeno
- Obsahuje číslici
- Obsahuje speciální znak

CSS třídy: `strength-weak` / `strength-fair` / `strength-good` / `strength-strong`

### Backend — `RegisterUserCommand`

```csharp
public sealed record RegisterUserCommand(
    string FirstName, string LastName, string Email,
    string Password, UserRole Role = UserRole.Client) : IRequest<UserResponse>;
```

**Validační pravidla** (`RegisterUserCommandValidator`):
- `FirstName` — NotEmpty, MaxLength(100)
- `LastName` — NotEmpty, MaxLength(100)
- `Email` — NotEmpty, formát emailu
- `Password` — NotEmpty, MinLength(8), musí obsahovat: `[A-Z]`, `[a-z]`, `\d`, speciální znak

**Flow registrace:**
1. Frontend odešle `RegisterRequest` (14 polí) na `POST /api/v1/auth/register`
2. Backend vytvoří `Email` value object (validace regex)
3. Kontrola duplikátu emailu v DB
4. Hash hesla (SHA256 → Base64)
5. `User.Create(...)` — factory metoda aggregate rootu
6. Uložení do DB přes `IUserRepository` + `IUnitOfWork`
7. Vrácení `UserResponse` DTO

### Shared model — `RegisterRequest`

```csharp
public sealed record RegisterRequest(
    string FirstName, string LastName, string Email,
    string Password, string PasswordConfirm, string Phone,
    DateTime DateOfBirth, string PersonalIdNumber,
    string Street, string City, string ZipCode, string Country);
```

---

## 2. Přihlášení (Login flow)

### Frontend — `Login.razor` (route: `/prihlaseni`)

- Pole: email, heslo
- Přepínání viditelnosti hesla (ikona oka)
- Enter klávesa odešle formulář
- Klientská validace: email musí obsahovat `@`, heslo nesmí být prázdné
- Po úspěšném přihlášení → navigace na `/prehled`

### Shared model — `LoginRequest` / `LoginResponse`

```csharp
public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string Token, Guid UserId, string Email,
    string FirstName, string LastName, string Role,
    Guid SessionId, DateTime ExpiresAt);
```

### Flow přihlášení:

```
Uživatel zadá email + heslo
        ↓
[Kontrola lockoutu] → Pokud je účet zamčený → zobraz "Účet dočasně zablokován"
        ↓
POST /api/v1/auth/login (LoginRequest)
        ↓
    ┌── Úspěch ──────────────────────────────────────┐
    │  Reset počtu pokusů                             │
    │  Vytvoření AuthSession (Token, SessionId, ...)  │
    │  Uložení do localStorage (fairbank_session)     │
    │  Start inactivity timeru (5 min)                │
    │  Navigace na /prehled                           │
    └─────────────────────────────────────────────────┘
    ┌── Neúspěch ────────────────────────────────────┐
    │  Inkrementace _failedAttempts                  │
    │  Pokud ≥ 5 → zamknutí na 5 minut              │
    │  Uložení stavu do localStorage                 │
    │  Zobrazení zbývajících pokusů                  │
    └────────────────────────────────────────────────┘
```

---

## 3. Limit přihlášení při chybných pokusech

Implementováno v `AuthService` (client-side):

| Konstanta | Hodnota |
|-----------|---------|
| `MaxLoginAttempts` | 5 |
| `LockoutMinutes` | 5 |

**Mechanismus:**
- Každý neúspěšný pokus inkrementuje čítač `_failedAttempts`
- Po dosažení 5 neúspěšných pokusů → účet se zamkne na 5 minut
- Stav se ukládá do **localStorage** (klíče: `fairbank_login_attempts`, `fairbank_locked_until`)
- Stav přežije refresh stránky
- Po vypršení lockoutu se čítač automaticky resetuje
- Frontend zobrazuje počet zbývajících pokusů (`RemainingAttempts`)
- Při lockoutu se zobrazí alert s ikonou 🔒

---

## 4. Automatický timeout (Inactivity)

| Konstanta | Hodnota |
|-----------|---------|
| `InactivityTimeoutMinutes` | 5 |

**Mechanismus:**
- Po přihlášení se spustí `System.Threading.Timer` na 5 minut
- Při jakékoliv aktivitě uživatele se volá `ResetInactivityTimer()` → restart timeru
- Po vypršení timeru:
  1. Session se vymaže z `localStorage`
  2. Vyvolá se event `AuthStateChanged`
  3. Uživatel je přesměrován na login s parametrem `?expired`
- Login stránka detekuje `?expired` a zobrazí upozornění "Session vypršela"

---

## 5. Jedna aktivní relace (Single Session)

**Koncept:**
- Každé přihlášení generuje unikátní `SessionId` (v `LoginResponse`)
- Session se ukládá jako `AuthSession` do localStorage
- Frontend periodicky validuje session voláním `GET /api/v1/auth/session/{sessionId}`
- Pokud server vrátí neúspěch (session invalidována) → automatický logout

**Flow při novém přihlášení z jiného zařízení:**
```
Zařízení A: přihlášen (SessionId = abc-123)
        ↓
Zařízení B: přihlášení stejným účtem → server vytvoří nový SessionId (xyz-789)
        ↓
Server invaliduje SessionId abc-123
        ↓
Zařízení A: validace session → server vrátí 401 → automatický logout
```

### `AuthSession` model

```csharp
public sealed record AuthSession(
    Guid SessionId, Guid UserId, string Token,
    string Email, string FirstName, string LastName,
    string Role, DateTime ExpiresAt);
```

---

## Doménový model — User Aggregate

```csharp
public sealed class User : AggregateRoot<Guid>
{
    // Properties (private set)
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public Email Email { get; private set; }          // Value Object
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }         // Enum: Client, Child, Banker, Admin
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    // Factory method
    public static User Create(string firstName, string lastName,
        Email email, string passwordHash, UserRole role);

    // Soft delete
    public void SoftDelete();
    public void Restore();
}
```

**Email Value Object:**
- Trim + lowercase
- Regex validace: `^[^@\s]+@[^@\s]+\.[^@\s]+$`

---

## Databázové schéma

Tabulka: `identity_service.users`

| Sloupec | Typ | Constraint |
|---------|-----|-----------|
| `Id` | `uuid` | PK |
| `FirstName` | `varchar(100)` | NOT NULL |
| `LastName` | `varchar(100)` | NOT NULL |
| `email` | `varchar(320)` | NOT NULL, UNIQUE INDEX |
| `PasswordHash` | `varchar(500)` | NOT NULL |
| `Role` | `varchar(20)` | NOT NULL (uloženo jako string) |
| `IsActive` | `boolean` | NOT NULL |
| `IsDeleted` | `boolean` | NOT NULL |
| `CreatedAt` | `timestamp` | NOT NULL |
| `UpdatedAt` | `timestamp` | nullable |
| `DeletedAt` | `timestamp` | nullable |

- **Global query filter:** `!IsDeleted` — soft-deleted uživatelé se automaticky filtrují
- **Unique index** na `email` sloupci

---

## API Endpointy

### Existující (Identity API)

| Metoda | Route | Popis |
|--------|-------|-------|
| `POST` | `/api/v1/users/register` | Registrace uživatele |
| `GET` | `/api/v1/users/{id:guid}` | Získání uživatele podle ID |
| `GET` | `/health` | Health check |

### Volané z frontendu (Auth flow)

| Metoda | Route | Popis |
|--------|-------|-------|
| `POST` | `/api/v1/auth/login` | Přihlášení |
| `POST` | `/api/v1/auth/logout` | Odhlášení |
| `POST` | `/api/v1/auth/register` | Registrace (rozšířená) |
| `GET` | `/api/v1/auth/session/{id}` | Validace session |

---

## Workflow — Jak to celé funguje dohromady

```
┌─────────────────────────────────────────────────────────────────────┐
│                        REGISTRACE                                   │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  1. Uživatel otevře /registrace                                     │
│  2. Vyplní rozšířený formulář (14 polí)                             │
│  3. Frontend validuje (věk ≥ 15, heslo silné, shoda hesel, ...)    │
│  4. POST /api/v1/auth/register → backend                           │
│  5. Backend: validace → kontrola duplikátu → hash hesla → uložení  │
│  6. Úspěch → zobrazí se potvrzení + odkaz na přihlášení            │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                        PŘIHLÁŠENÍ                                   │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  1. Uživatel otevře /prihlaseni                                     │
│  2. Zadá email + heslo                                              │
│  3. Kontrola lockoutu (max 5 pokusů / 5 min blokace)               │
│  4. POST /api/v1/auth/login → backend                              │
│  5. Backend ověří credentials, vrátí Token + SessionId              │
│  6. Frontend uloží AuthSession do localStorage                      │
│  7. Spustí se inactivity timer (5 min)                              │
│  8. Navigace na /prehled (dashboard)                                │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                   AKTIVNÍ SESSION                                   │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  • Každá akce uživatele → ResetInactivityTimer()                   │
│  • Pokud 5 min bez aktivity → automatický logout + redirect        │
│  • Session se periodicky validuje proti serveru                     │
│  • Pokud se uživatel přihlásí jinde → stará session = invalidní    │
│  • Pouze 1 aktivní session na uživatele                            │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────┐
│                       ODHLÁŠENÍ                                     │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  • Manuální: uživatel klikne Odhlásit                              │
│  • Automatické: inactivity timeout (5 min)                          │
│  • Vynucené: přihlášení z jiného zařízení                          │
│  → POST /api/v1/auth/logout                                        │
│  → Vymazání session z localStorage                                  │
│  → Redirect na /prihlaseni                                          │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Soubory zapojené v implementaci

### Backend (Identity Service)
- `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs`
- `src/Services/Identity/FairBank.Identity.Domain/ValueObjects/Email.cs`
- `src/Services/Identity/FairBank.Identity.Domain/Enums/UserRole.cs`
- `src/Services/Identity/FairBank.Identity.Domain/Ports/IUserRepository.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommand.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandHandler.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandValidator.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetUserById/GetUserByIdQuery.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/UserResponse.cs`
- `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/IdentityDbContext.cs`
- `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs`
- `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`
- `src/Services/Identity/FairBank.Identity.Api/Program.cs`

### Frontend (Auth UI)
- `src/FairBank.Web.Auth/Pages/Login.razor`
- `src/FairBank.Web.Auth/Pages/Register.razor`

### Sdílené (Web Shared)
- `src/FairBank.Web.Shared/Models/LoginRequest.cs`
- `src/FairBank.Web.Shared/Models/LoginResponse.cs`
- `src/FairBank.Web.Shared/Models/RegisterRequest.cs`
- `src/FairBank.Web.Shared/Models/AuthSession.cs`
- `src/FairBank.Web.Shared/Services/IAuthService.cs`
- `src/FairBank.Web.Shared/Services/AuthService.cs`
- `src/FairBank.Web.Shared/Services/IFairBankApi.cs`
- `src/FairBank.Web.Shared/Services/FairBankApiClient.cs`

### Testy
- `tests/FairBank.Identity.UnitTests/` — 20 unit testů (domain + application)
