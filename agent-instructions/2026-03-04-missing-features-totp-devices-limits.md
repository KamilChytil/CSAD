# 2026-03-04 — TOTP 2FA, Device Management, Financial Limits, Frontend Integration

## Souhrn změn

Doplnění 3 zbývajících features z 10 požadavků aktéra "Uživatel (klient)": dvoufaktorová autentizace (TOTP), správa přihlášených zařízení, vynucování finančních limitů při platbách. Plus frontend integrace pro všechny nové backend capabilities.

**~65 nových/změněných souborů, 72 nových unit testů, 140/140 testů PASSED**

---

## Workflow

### Fáze 1: Hloubková analýza (3× Explore agent paralelně)

Každý agent dostal jednu oblast:
- **Agent A** — Accounts + Payments service (event sourcing, Marten, CQRS vzory)
- **Agent B** — Identity service (entity, commands, handlers, migrations, DTOs)
- **Agent C** — Payments, Chat, Docker, frontend, shared modely

Výstup: Pokrytí 10 požadavků přehodnoceno z ~41% na ~75%. Mnoho features (Cards 100%, Notifications 100%, Chat improvements 100%, Email verification 100%) bylo již hotovo ale nebylo to viditelné bez analýzy.

### Fáze 2: Interaktivní design (brainstorming skill)

Design schválen uživatelem po 4 sekcích. Uložen do `docs/plans/2026-03-04-fairbank-missing-features-design.md`.

### Fáze 3: Implementační plán (writing-plans skill + 3× Explore agent)

Plán s 12 tasky, exact file paths, kompletní kód. Uložen do `docs/plans/2026-03-04-remaining-features-plan.md`.

### Fáze 4: Subagent-Driven Development

**Sekvenční implementace** (4 general-purpose agenti, jeden po druhém):
1. TOTP 2FA implementer → spec review agent → fix error handling
2. Device Management implementer
3. Limits Enforcement implementer
4. Frontend: 2 paralelní agenti (DTOs+API client ‖ UI components)

**Paralelní testy** (2 agenti):
- Identity unit tests (34 testů)
- Payments unit tests (38 testů)

### Fáze 5: Verifikace

Docker build všech 3 services + `dotnet test` v Docker containeru. Nalezeny a opraveny 4 build problémy (Dockerfile, Razor `&quot;` entity).

---

## 1. Two-Factor Authentication (TOTP) — Identity Service

RFC 6238 TOTP s backup kódy, integrováno do login flow.

### Domain

**`src/Services/Identity/FairBank.Identity.Domain/Entities/TwoFactorAuth.cs`**
- Extends `Entity<Guid>`
- Properties: UserId, SecretKey, IsEnabled, BackupCodes (JSON array hashovaných kódů), CreatedAt, EnabledAt
- Factory: `Create(userId, secretKey)` — vždy validní stav
- Metody: `Enable(hashedBackupCodes)`, `Disable()`, `RegenerateBackupCodes(hashedBackupCodes)`
- Invarianty: Enable na enabled → throw, Disable na disabled → throw, Regenerate na disabled → throw

**`src/Services/Identity/FairBank.Identity.Domain/Ports/ITwoFactorAuthRepository.cs`**
- `GetByUserIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteByUserIdAsync`

**Modifikace `User.cs`:**
- Přidáno: `IsTwoFactorEnabled` property, `EnableTwoFactor()`, `DisableTwoFactor()`

### Application

**`src/Services/Identity/FairBank.Identity.Application/Helpers/TotpHelper.cs`**
- Statická třída, zero dependencies
- `GenerateSecret()` — 20 bajtů → Base32 (RFC 4648)
- `VerifyCode(secret, code)` — HMAC-SHA1, 30s time step, ±1 drift tolerance
- `GetOtpAuthUri(secret, email, issuer)` — `otpauth://totp/FairBank:email?secret=...`
- `GenerateBackupCodes(count=8)` — 8-digit numeric kódy
- Private: `Base32Encode`, `Base32Decode`, `GenerateCode` (big-endian 8-byte counter → HMAC → dynamic truncation)

**`src/Services/Identity/FairBank.Identity.Application/Users/DTOs/TwoFactorSetupResponse.cs`**
- Record: Secret, OtpAuthUri, IsAlreadyEnabled

**Commands (4):**

| Command | Handler logika |
|---------|---------------|
| `SetupTwoFactorCommand(UserId)` | Generuje secret, vytvoří TwoFactorAuth entity, vrátí setup response. Pokud enabled → `IsAlreadyEnabled: true`. Pokud existuje non-enabled → smaže a recreate. |
| `EnableTwoFactorCommand(UserId, Code)` | Verifikuje TOTP kód, generuje 8 backup kódů (BCrypt hash, workFactor: 10), povolí 2FA na entity i User. Vrátí plaintext backup kódy (jednorázově). |
| `DisableTwoFactorCommand(UserId, Code)` | Verifikuje kód (TOTP nebo backup), zakáže 2FA. |
| `VerifyTwoFactorCommand(UserId, Code)` | Login-time verifikace. Zkusí TOTP, pak backup kódy. Invaliduje použitý backup kód (→ empty string). Vytvoří session (`RecordSuccessfulLogin`), vrátí `LoginResponse`. |

**Modifikace `LoginUserCommandHandler.cs`:**
- Po úspěšném ověření hesla a emailu: if `IsTwoFactorEnabled` → vrátí partial `LoginResponse` s `RequiresTwoFactor: true`, prázdný token, `Guid.Empty` session. Žádná session se nevytvoří.

**Modifikace `LoginResponse.cs`:**
- Přidáno: `RequiresTwoFactor` init-only property

### Infrastructure

**`src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/TwoFactorAuthConfiguration.cs`**
- Tabulka: `two_factor_auth`
- Unique index na UserId
- SecretKey max 200, BackupCodes max 4000

**`src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/TwoFactorAuthRepository.cs`**
- EF Core implementace, `ExecuteDeleteAsync` pro bulk delete

**Modifikace:**
- `IdentityDbContext.cs` — `DbSet<TwoFactorAuth> TwoFactorAuths`
- `DependencyInjection.cs` — `ITwoFactorAuthRepository` → `TwoFactorAuthRepository`
- `UserConfiguration.cs` — `IsTwoFactorEnabled` default false

### API Endpoints

**`src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`** — 4 nové:

| Metoda | Cesta | Popis | Error handling |
|--------|-------|-------|----------------|
| `POST` | `/api/v1/users/2fa/setup` | Inicializace 2FA | try/catch → BadRequest |
| `POST` | `/api/v1/users/2fa/enable` | Aktivace 2FA, vrátí backup kódy | try/catch → BadRequest |
| `POST` | `/api/v1/users/2fa/disable` | Deaktivace 2FA | try/catch → BadRequest |
| `POST` | `/api/v1/users/2fa/verify` | Verifikace při loginu | try/catch → BadRequest |

### Testy (34)

| Soubor | Testů | Co testuje |
|--------|-------|-----------|
| `Helpers/TotpHelperTests.cs` | 10 | Base32, secret generace, URI formát, backup kódy, round-trip |
| `Application/SetupTwoFactorCommandHandlerTests.cs` | 4 | Setup flow, already-enabled, non-enabled cleanup, user not found |
| `Application/EnableTwoFactorCommandHandlerTests.cs` | 4 | Enable s reálným TOTP kódem, invalid kód, already enabled, no setup |
| `Application/DisableTwoFactorCommandHandlerTests.cs` | 4 | Disable s reálným TOTP kódem, invalid, not enabled, no setup |
| `Domain/TwoFactorAuthTests.cs` | 7 | Create defaults, Enable/Disable, invarianty, RegenerateBackupCodes |

---

## 2. Device Management — Identity Service

Tracking přihlášených zařízení s fingerprinting, trust management, remote revoke.

### Domain

**`src/Services/Identity/FairBank.Identity.Domain/Entities/UserDevice.cs`**
- Properties: UserId, DeviceName, DeviceType (Desktop/Mobile/Tablet), Browser, OperatingSystem, IpAddress, SessionId, IsTrusted, IsCurrentDevice, LastActiveAt, CreatedAt
- Factory: `Create(userId, deviceName, deviceType, browser, os, ip, sessionId)`
- Metody: `UpdateActivity(ip, sessionId)`, `MarkTrusted()`, `UnmarkTrusted()`, `Revoke()`
- Revoke: `SessionId = null`, `IsCurrentDevice = false`

**`src/Services/Identity/FairBank.Identity.Domain/Ports/IUserDeviceRepository.cs`**
- `GetByUserIdAsync`, `GetByIdAsync`, `FindByFingerprintAsync(userId, browser, os, deviceType)`, `AddAsync`, `UpdateAsync`, `DeleteAsync`

### Application

**`src/Services/Identity/FairBank.Identity.Application/Users/DTOs/DeviceResponse.cs`**
- Record: Id, DeviceName, DeviceType, Browser, OperatingSystem, IpAddress, IsTrusted, IsCurrentDevice, LastActiveAt, CreatedAt

**Commands + Query (4):**

| Command/Query | Logika |
|---------------|--------|
| `RegisterDeviceCommand` | Fingerprint match (browser+OS+type) → update activity. Nový → create. |
| `RevokeDeviceCommand(DeviceId, UserId)` | Validuje ownership, invaliduje session na User pokud matchuje (`InvalidateSession`), revoke device. |
| `TrustDeviceCommand(DeviceId, UserId)` | Validuje ownership, `MarkTrusted()`. |
| `GetDevicesQuery(UserId)` | Vrátí všechna zařízení uživatele. |

### Infrastructure

**`src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserDeviceConfiguration.cs`**
- Tabulka: `user_devices`
- Index na UserId
- Composite fingerprint index: `ix_user_devices_fingerprint` (UserId, Browser, OS, DeviceType)

**`src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/UserDeviceRepository.cs`**
- EF Core, řazení `LastActiveAt` descending

**Modifikace:**
- `IdentityDbContext.cs` — `DbSet<UserDevice>`
- `DependencyInjection.cs` — `IUserDeviceRepository` registrace

### API Endpoints

| Metoda | Cesta | Popis | Auth |
|--------|-------|-------|------|
| `GET` | `/api/v1/users/{userId}/devices` | Seznam zařízení | Route param |
| `POST` | `/api/v1/users/devices` | Registrace zařízení | Body |
| `DELETE` | `/api/v1/users/devices/{id}` | Remote logout | Bearer token → `SessionTokenHelper.TryDecode` |
| `PUT` | `/api/v1/users/devices/{id}/trust` | Označit důvěryhodné | Bearer token → `SessionTokenHelper.TryDecode` |

### Testy (11)

| Soubor | Testů | Co testuje |
|--------|-------|-----------|
| `Application/RegisterDeviceCommandHandlerTests.cs` | 3 | Nový device, existing fingerprint update, response mapping |
| `Application/RevokeDeviceCommandHandlerTests.cs` | 3 | Revoke, non-existent, wrong user |
| `Application/TrustDeviceCommandHandlerTests.cs` | 3 | Trust, non-existent, wrong user |
| `Domain/UserDeviceTests.cs` | 5 | Create defaults, UpdateActivity, Mark/Unmark trusted, Revoke |

---

## 3. Financial Limits Enforcement — Payments Service

Vynucování 5 typů limitů před zpracováním platby.

### Application

**`src/Services/Payments/FairBank.Payments.Application/Services/LimitEnforcementService.cs`**
- Statická třída, 5 metod:
  - `EnforceSingleTransactionLimit(amount, limit)` — max jedna platba
  - `EnforceDailyLimit(todayTotal, amount, dailyLimit)` — max denní objem
  - `EnforceMonthlyLimit(monthTotal, amount, monthlyLimit)` — max měsíční objem
  - `EnforceDailyCount(todayCount, maxCount)` — max počet plateb/den
  - `EnforceNightRestriction(nightEnabled)` — blokace 23:00–06:00
- Všechny nullable limity: `null` → skip check
- Chybové hlášky v češtině, `InvalidOperationException`

### Modifikace

**`IAccountsServiceClient.cs`:**
- Přidáno: `GetAccountLimitsAsync(accountId)` + `AccountLimitsInfo` record (Daily, Monthly, Single, Count, Online limity)

**`AccountsServiceHttpClient.cs`:**
- HTTP volání na existující endpoint `GET /api/v1/accounts/{id}/limits`
- Mapping API response → `AccountLimitsInfo` DTO

**`IPaymentRepository.cs`:**
- Přidáno: `GetSentTotalsAsync(senderAccountId, from)` → `(decimal TotalAmount, int Count)`

**`PaymentRepository.cs`:**
- Efektivní EF Core dotazy: `SumAsync` + `CountAsync` (aggregace na DB, ne v paměti)

**`SendPaymentCommandHandler.cs`:**
- Step 3a (mezi balance check a child spending limit):
  1. Fetch account limits via `GetAccountLimitsAsync`
  2. Night restriction check
  3. Single transaction limit
  4. Daily/monthly totals via `GetSentTotalsAsync` (today start, month start)
  5. Enforce daily limit, monthly limit, daily count

### Testy (38 — parameterizované Theory/InlineData)

| Soubor | Metod | Test cases | Co testuje |
|--------|-------|-----------|-----------|
| `Services/LimitEnforcementServiceTests.cs` | 18 | 38 | Všech 5 metod: within limit, exceeds, null, boundary (exact-at-limit), night restriction |

---

## 4. Frontend Integration

### Shared DTOs (3 nové soubory)

| Soubor | Properties |
|--------|-----------|
| `Models/TwoFactorSetupResponse.cs` | Secret, OtpAuthUri, IsAlreadyEnabled |
| `Models/DeviceDto.cs` | Id, DeviceName, DeviceType, Browser, OS, IP, IsTrusted, IsCurrentDevice, LastActiveAt, CreatedAt |
| `Models/NotificationPreferenceDto.cs` | TransactionNotifications, SecurityNotifications, CardNotifications, LimitNotifications, ChatNotifications, EmailNotificationsEnabled |

### API Client (`IFairBankApi.cs` + `FairBankApiClient.cs`)

10 nových metod:

| Kategorie | Metody |
|-----------|--------|
| 2FA | `SetupTwoFactorAsync`, `EnableTwoFactorAsync`, `DisableTwoFactorAsync`, `VerifyTwoFactorAsync` |
| Devices | `GetDevicesAsync`, `RevokeDeviceAsync`, `TrustDeviceAsync` |
| Cards | `SetCardPinAsync` (ostatní existovaly) |
| Notifications | `GetNotificationPreferencesAsync`, `UpdateNotificationPreferencesAsync` |

### Profile Page (`Profile.razor` — 174→911 řádků)

4 nové sekce:

**2FA sekce ("Dvoufaktorové ověření"):**
- Status badge (enabled/disabled)
- Enable flow: setup → zobrazí secret + OTP URI → verify 6-digit kód → zobrazí backup kódy
- Disable flow: prompt pro TOTP kód → confirm
- Loading states, error handling

**Devices sekce ("Přihlášená zařízení"):**
- Seznam zařízení: name, browser, OS, IP, last active
- Current device: zelený highlight + badge
- Trusted: zlatý badge
- Trust/Untrust toggle, Revoke s potvrzením

**Security settings ("Zabezpečení"):**
- Night transactions toggle
- International payments toggle

**Notification preferences ("Notifikace"):**
- 6 toggleů (Transaction, Security, Card, Limit, Chat, Email)
- Auto-save s "Uloženo" indikátorem

### Login 2FA Flow (`Login.razor`)

- Po úspěšném loginu: check `RequiresTwoFactor`
- Pokud true → switch na TOTP input view (6-digit, `inputmode="numeric"`)
- Verify → `VerifyTwoFactorAsync` → re-login pro session establishment
- Enter key handling, error messages v češtině

### Navigation

- `BottomNav.razor` — přidána "Karty" položka s `wallet` ikonou
- `SideNav.razor` — přidána "Karty" položka s `wallet` ikonou
- `App.razor` — registrace `FairBank.Web.Cards` assembly

---

## 5. Docker & Build Fixes

| Soubor | Problém | Řešení |
|--------|---------|--------|
| `src/FairBank.Web/Dockerfile` | Chyběl COPY pro `FairBank.Web.Cards` | Přidány 2 COPY řádky (restore + build fáze) |
| `src/FairBank.Web/Pages/Admin.razor` | `$&quot;...&quot;` v Razor C# kontextu | `string.Concat(a, " ", b)` |
| `src/FairBank.Web/Pages/Family.razor` | `&quot;/deti/...&quot;` v onclick | Single-quote attribute |
| `src/FairBank.Web.Products/Pages/Management.razor` | `&quot;/zpravy&quot;` v onclick | Single-quote attribute |

---

## Build & Test Evidence

```
docker compose build identity-api    → 0 errors, 0 warnings
docker compose build payments-api    → 0 errors, 0 warnings
docker compose build web-app         → 0 errors, 0 warnings

dotnet test FairBank.Identity.UnitTests   → 96/96 PASSED (34 new)
dotnet test FairBank.Payments.UnitTests   → 44/44 PASSED (38 new)
```

---

## Co chybí pro kompletní nasazení

1. **EF Core migrace** — `AddTwoFactorAuth`, `AddUserDevice` pro Identity service. Bez nich nové tabulky neexistují v DB.
2. **`docker compose up -d --build`** — restart s novými images
3. **Notification triggery** — při přihlášení z nového zařízení, blokaci karty, dosažení limitu
4. **E2E testy** — kompletní login→2FA→dashboard flow
