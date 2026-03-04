# FairBank — Implementation Report

**Datum:** 2026-03-04
**Metoda:** Subagent-Driven Development (Claude Opus 4.6)
**Zdroj:** `docs/plans/2026-03-04-fairbank-missing-features-design.md`

---

## Souhrn

Implementace 3 zbývajících features + frontend integrace pro bankovní aplikaci FairBank. Celkem **72 nových unit testů**, všechny 3 backendy a frontend úspěšně buildují.

---

## Fáze 1: Analýza (3 paralelní Explore agenti)

**Cíl:** Hloubková analýza 10 požadavků aktéra "Uživatel (klient)" proti existujícímu kódu.

| Agent | Oblast | Zjištění |
|-------|--------|----------|
| Explore #1 | Accounts + Payments service | Event sourcing (Marten), CQRS, AccountLimits value object existuje |
| Explore #2 | Identity service | Full KYC, email verifikace, password management — vše hotovo |
| Explore #3 | Payments, Chat, Docker, Frontend | Cards microservice 100%, Notifications 100%, Chat 100% |

**Klíčový závěr:** Původní odhad ~41% pokrytí byl přehodnocen na ~75%. Mnoho features bylo již implementováno, ale nebylo to viditelné bez hloubkové analýzy.

**Zbývající práce:**
1. Two-Factor Authentication (TOTP) — 0%
2. Device Management — 0%
3. Financial Limits Enforcement — 50% (struktura existuje, chybí enforcement)
4. Frontend Integration — DTOs, API client metody, UI rozšíření

---

## Fáze 2: Design (interaktivní, schváleno uživatelem po sekcích)

**Výstup:** `docs/plans/2026-03-04-fairbank-missing-features-design.md`

4 sekce schváleny jedna po druhé:
1. Identita & Bezpečnost (registrace, 2FA, reset hesla, zařízení)
2. Bankovní operace (účty, platby, QR, karty)
3. Limity & Monitoring (finanční limity, statistiky)
4. Komunikace & Notifikace (notifikace, chat)

---

## Fáze 3: Plánování (writing-plans skill + 3 Explore agenti)

**Výstup:** `docs/plans/2026-03-04-remaining-features-plan.md`

Detailní implementační plán s 12 tasky, exact file paths, kompletní kód.

---

## Fáze 4: Implementace (Subagent-Driven Development)

### Task 3: Two-Factor Authentication (TOTP)

**Agent:** general-purpose implementer
**Trvání:** ~8.5 min
**Instrukce:**
> Implement TOTP 2FA for FairBank Identity microservice. Create domain entity (TwoFactorAuth), repository port, TOTP helper (RFC 6238 HMAC-SHA1, Base32), 4 command handlers (Setup, Enable, Disable, Verify), modify LoginUserCommandHandler for 2FA flow, add infrastructure (EF config, repository, DI), add 4 API endpoints. Match existing Clean Architecture patterns. Build to verify.

**Vytvořené soubory (14):**

Domain:
- `src/Services/Identity/FairBank.Identity.Domain/Entities/TwoFactorAuth.cs`
- `src/Services/Identity/FairBank.Identity.Domain/Ports/ITwoFactorAuthRepository.cs`

Application:
- `src/Services/Identity/FairBank.Identity.Application/Helpers/TotpHelper.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/TwoFactorSetupResponse.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/SetupTwoFactor/SetupTwoFactorCommand.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/SetupTwoFactor/SetupTwoFactorCommandHandler.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/EnableTwoFactor/EnableTwoFactorCommand.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/EnableTwoFactor/EnableTwoFactorCommandHandler.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/DisableTwoFactor/DisableTwoFactorCommand.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/DisableTwoFactor/DisableTwoFactorCommandHandler.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/VerifyTwoFactor/VerifyTwoFactorCommand.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/VerifyTwoFactor/VerifyTwoFactorCommandHandler.cs`

Infrastructure:
- `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/TwoFactorAuthConfiguration.cs`
- `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/TwoFactorAuthRepository.cs`

**Modifikované soubory (6):**
- `User.cs` — přidáno `IsTwoFactorEnabled`, `EnableTwoFactor()`, `DisableTwoFactor()`
- `LoginResponse.cs` — přidáno `RequiresTwoFactor` init property
- `LoginUserCommandHandler.cs` — 2FA check po ověření hesla
- `IdentityDbContext.cs` — `DbSet<TwoFactorAuth>`
- `DependencyInjection.cs` — DI registrace
- `UserConfiguration.cs` — `IsTwoFactorEnabled` default false
- `UserEndpoints.cs` — 4 nové endpointy s error handling

**Spec Review Agent:** PASS — plná shoda se specifikací, 0 chybějících položek
**Nalezený problém:** Chyběl error handling v API endpointech → opraven (try/catch s BadRequest)

---

### Task 4: Device Management

**Agent:** general-purpose implementer
**Trvání:** ~6.9 min
**Instrukce:**
> Implement Device Management for FairBank Identity service. Create UserDevice entity with fingerprinting, repository port, 4 commands (Register, Revoke, Trust, GetDevices), infrastructure (EF config, repository, DI, migration config), API endpoints. Adapt to actual codebase patterns (ActiveSessionId/InvalidateSession vs CurrentSessionId/Logout).

**Vytvořené soubory (13):**

Domain:
- `src/Services/Identity/FairBank.Identity.Domain/Entities/UserDevice.cs`
- `src/Services/Identity/FairBank.Identity.Domain/Ports/IUserDeviceRepository.cs`

Application:
- `src/Services/Identity/FairBank.Identity.Application/Users/DTOs/DeviceResponse.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterDevice/RegisterDeviceCommand.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterDevice/RegisterDeviceCommandHandler.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RevokeDevice/RevokeDeviceCommand.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RevokeDevice/RevokeDeviceCommandHandler.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/TrustDevice/TrustDeviceCommand.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/TrustDevice/TrustDeviceCommandHandler.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetDevices/GetDevicesQuery.cs`
- `src/Services/Identity/FairBank.Identity.Application/Users/Queries/GetDevices/GetDevicesQueryHandler.cs`

Infrastructure:
- `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserDeviceConfiguration.cs`
- `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Repositories/UserDeviceRepository.cs`

**Modifikované soubory (3):**
- `IdentityDbContext.cs` — `DbSet<UserDevice>`
- `DependencyInjection.cs` — `IUserDeviceRepository` registrace
- `UserEndpoints.cs` — 4 device endpointy s Bearer token autentizací

---

### Task 7: Financial & Security Limits Enforcement

**Agent:** general-purpose implementer
**Trvání:** ~7.8 min
**Instrukce:**
> Implement limit enforcement in Payments service. Create LimitEnforcementService (single/daily/monthly/count/night). Add GetAccountLimitsAsync to IAccountsServiceClient. Add GetSentTotalsAsync to IPaymentRepository for efficient aggregation. Modify SendPaymentCommandHandler to enforce limits before payment processing.

**Vytvořené soubory (1):**
- `src/Services/Payments/FairBank.Payments.Application/Services/LimitEnforcementService.cs`

**Modifikované soubory (4):**
- `IAccountsServiceClient.cs` — `GetAccountLimitsAsync()` + `AccountLimitsInfo` record
- `AccountsServiceHttpClient.cs` — implementace HTTP volání na `/api/v1/accounts/{id}/limits`
- `IPaymentRepository.cs` — `GetSentTotalsAsync()` pro efektivní SUM/COUNT
- `PaymentRepository.cs` — EF Core implementace s `SumAsync`/`CountAsync`
- `SendPaymentCommandHandler.cs` — limit enforcement před zpracováním platby

---

### Task 12: Frontend Integration (2 paralelní agenti)

#### Agent A: DTOs + API Client
**Trvání:** ~2.4 min
**Instrukce:**
> Add shared DTOs and API client methods. Create TwoFactorSetupResponse, DeviceDto, NotificationPreferenceDto. Add RequiresTwoFactor to LoginResponse. Add 10 new methods to IFairBankApi + FairBankApiClient (2FA, Devices, Card PIN, Notification preferences).

**Vytvořené soubory (3):**
- `src/FairBank.Web.Shared/Models/TwoFactorSetupResponse.cs`
- `src/FairBank.Web.Shared/Models/DeviceDto.cs`
- `src/FairBank.Web.Shared/Models/NotificationPreferenceDto.cs`

**Modifikované soubory (3):**
- `LoginResponse.cs` — `RequiresTwoFactor` parametr
- `IFairBankApi.cs` — 10 nových metod
- `FairBankApiClient.cs` — 10 implementací

#### Agent B: UI Components
**Trvání:** ~9.4 min
**Instrukce:**
> Expand Profile.razor with 2FA setup wizard, device list, security settings, notification preferences. Modify Login.razor for 2FA verification flow. Add Cards navigation. Match existing Blazor patterns (ContentCard, ToggleSwitch, VbButton, CSS variables).

**Modifikované soubory (5):**
- `Profile.razor` — 174→911 řádků (2FA sekce, Devices sekce, Security, Notification preferences)
- `Login.razor` — 2FA kód input po úspěšném loginu s `RequiresTwoFactor`
- `BottomNav.razor` — "Karty" navigační položka
- `SideNav.razor` — "Karty" navigační položka
- `App.razor` — `FairBank.Web.Cards` assembly registrace

---

## Fáze 5: Unit Testy (2 paralelní agenti)

#### Agent: Identity Unit Tests
**Trvání:** ~4.6 min
**Instrukce:**
> Write unit tests for 2FA and Device Management. Test TotpHelper (10 tests), SetupTwoFactor handler (4), EnableTwoFactor handler (4), DisableTwoFactor handler (4), RegisterDevice handler (3), RevokeDevice handler (3), TrustDevice handler (3), TwoFactorAuth entity (7), UserDevice entity (5). Use xUnit, NSubstitute, FluentAssertions.

**Vytvořené soubory (9):**
- `tests/FairBank.Identity.UnitTests/Helpers/TotpHelperTests.cs` (10 testů)
- `tests/FairBank.Identity.UnitTests/Application/SetupTwoFactorCommandHandlerTests.cs` (4 testy)
- `tests/FairBank.Identity.UnitTests/Application/EnableTwoFactorCommandHandlerTests.cs` (4 testy)
- `tests/FairBank.Identity.UnitTests/Application/DisableTwoFactorCommandHandlerTests.cs` (4 testy)
- `tests/FairBank.Identity.UnitTests/Application/RegisterDeviceCommandHandlerTests.cs` (3 testy)
- `tests/FairBank.Identity.UnitTests/Application/RevokeDeviceCommandHandlerTests.cs` (3 testy)
- `tests/FairBank.Identity.UnitTests/Application/TrustDeviceCommandHandlerTests.cs` (3 testy)
- `tests/FairBank.Identity.UnitTests/Domain/TwoFactorAuthTests.cs` (7 testů)
- `tests/FairBank.Identity.UnitTests/Domain/UserDeviceTests.cs` (5 testů)

**Výsledek:** 96/96 PASSED (34 nových + 62 existujících)

#### Agent: Payments Unit Tests
**Trvání:** ~2.3 min
**Instrukce:**
> Write unit tests for LimitEnforcementService. Test all 5 methods with Theory/InlineData: single limit (4), daily limit (4), monthly limit (3), daily count (3), night restriction (2). Boundary testing.

**Vytvořené soubory (1):**
- `tests/FairBank.Payments.UnitTests/Services/LimitEnforcementServiceTests.cs` (38 testů)

**Výsledek:** 44/44 PASSED (38 nových + 6 existujících)

---

## Fáze 6: Verifikace

| Ověření | Výsledek |
|---------|----------|
| `docker compose build identity-api` | 0 errors, 0 warnings |
| `docker compose build payments-api` | 0 errors, 0 warnings |
| `docker compose build web-app` | 0 errors (po opravách) |
| `dotnet test Identity.UnitTests` | 96/96 PASSED |
| `dotnet test Payments.UnitTests` | 44/44 PASSED |

**Opravené problémy při verifikaci:**
1. `Dockerfile` — chyběl COPY pro `FairBank.Web.Cards` (nový microservice)
2. `Admin.razor` — `&quot;` HTML entity v Razor C# kontextu → `string.Concat()`
3. `Family.razor` — stejný problém → single-quote attribute
4. `Management.razor` — stejný problém → single-quote attribute

---

## Statistiky

| Metrika | Hodnota |
|---------|---------|
| Nové soubory | ~45 |
| Modifikované soubory | ~20 |
| Nové unit testy | 72 |
| Celkem testů (Identity) | 96 |
| Celkem testů (Payments) | 44 |
| Agentů spuštěno | 11 (3 explore + 1 spec review + 4 implementer + 2 test writer + 1 verifikace) |
| Paralelní běhy | 5× (explore 3×, frontend 2×, testy 2×) |

---

## Co zbývá

1. **EF Core migrace** — `AddTwoFactorAuth` a `AddUserDevice` migrace pro Identity service
2. **Docker Compose restart** — `docker compose up -d --build` pro nasazení nových images
3. **Integrace s Notification service** — triggery notifikací při přihlášení z nového zařízení, blokaci karty, dosažení limitu
4. **E2E testy** — Selenium/Playwright testy pro kompletní flow
