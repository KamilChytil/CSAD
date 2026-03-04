# 2026-03-04 — Opravy chyb a unikátní čísla účtů

## Přehled změn

Tato session opravila sérii chyb nalezených při testování a doimplementovala generování unikátních čísel účtů.

---

## 1. Oprava registrace a přihlášení

### Problém
- `POST /api/v1/users/register` vracel **400** — backend očekává `DateOnly`, ale frontend posílal plný ISO datetime string.
- `POST /api/v1/auth/login` vracel **500** — při registraci se volalo `GenerateEmailVerificationToken()`, což nastavilo příznak `IsEmailVerified = false`; login pak blokoval přihlášení neověřeného uživatele.

### Oprava
- `RegisterRequest.DateOfBirth` změněno z `DateTime` na `string`; hodnota se formátuje jako `yyyy-MM-dd` před odesláním.
- Z `RegisterUserCommandHandler` odstraněno volání `user.GenerateEmailVerificationToken()` a závislost na `IEmailSender` — nový uživatel je rovnou ověřen.
- Stávající zablokovaní uživatelé odemčeni SQL skriptem (`docker/verify_users.sql`).

**Dotčené soubory:**
- `src/FairBank.Web.Shared/Models/RegisterRequest.cs`
- `src/FairBank.Web.Auth/Pages/Register.razor`
- `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandHandler.cs`

---

## 2. Automatické zakládání účtu po registraci

### Problém
Noví uživatelé neměli po registraci žádný bankovní účet — identita nikdy nevolala accounts-api.

### Oprava
Endpoint `/register` (a `/{parentId}/children`) v `UserEndpoints.cs` po úspěšné registraci automaticky zavolá accounts-api (`POST /api/v1/accounts`) s `OwnerId` a měnou `CZK`. Volání je obaleno v try-catch, aby selhání accounts-api neovlivnilo registraci samotnou.

V `Program.cs` identity-api byl zaregistrován pojmenovaný `HttpClient` s názvem `"accounts-api"` a v `docker-compose.yml` přidána proměnná prostředí `Services__AccountsApi`.

**Dotčené soubory:**
- `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs`
- `src/Services/Identity/FairBank.Identity.Api/Program.cs`
- `docker-compose.yml`

---

## 3. Unikátní čísla účtů (DB sekvence)

### Problém
Čísla účtů se generovala pomocí `Random.Shared.NextInt64(...)` — při větším počtu účtů hrozily kolize.

### Řešení
Vytvořena PostgreSQL sekvence `accounts_service.account_number_seq` a port interface `IAccountNumberGenerator`. Implementace `PostgresAccountNumberGenerator` čte `nextval()` z DB a mapuje hodnotu na český formát čísla účtu:

```
seq 1 – 9 999 999 999  →  000000-{seq:D10}/8888
seq 10 000 000 000+    →  {prefix:D6}-{číslo:D10}/8888  (prefix se inkrementuje)
```

Algoritmus:
```
prefix = (seq - 1) / 9_999_999_999
číslo  = ((seq - 1) % 9_999_999_999) + 1
```

`CreateAccountCommandHandler` nyní generátor injectuje; pokud `request.AccountNumber` není zadáno explicitně, zavolá `await numberGenerator.NextAsync(ct)`.

**Dotčené/nové soubory:**
- `src/Services/Accounts/FairBank.Accounts.Application/Ports/IAccountNumberGenerator.cs` *(nový)*
- `src/Services/Accounts/FairBank.Accounts.Infrastructure/Persistence/PostgresAccountNumberGenerator.cs` *(nový)*
- `src/Services/Accounts/FairBank.Accounts.Infrastructure/DependencyInjection.cs`
- `src/Services/Accounts/FairBank.Accounts.Application/Commands/CreateAccount/CreateAccountCommandHandler.cs`

**SQL (spustit na DB):**
```sql
CREATE SEQUENCE IF NOT EXISTS accounts_service.account_number_seq
    START WITH 1 INCREMENT BY 1 NO MAXVALUE NO CYCLE;
GRANT USAGE, SELECT ON SEQUENCE accounts_service.account_number_seq TO fairbank_app;
```

---

## 4. Správná chybová hlášení při nedostatku prostředků

### Problém
Platba nebo trvalý příkaz bez dostatku prostředků vracel **500** místo srozumitelné chyby.

### Oprava
Oba endpointy (`PaymentEndpoints`, `StandingOrderEndpoints`) nyní zachytávají `InvalidOperationException` a vracejí **400** s tělem `{ "error": "<zpráva>" }`. Frontend (`FairBankApiClient`) čte toto tělo a zobrazuje ho uživateli.

**Dotčené soubory:**
- `src/Services/Payments/FairBank.Payments.Api/Endpoints/PaymentEndpoints.cs`
- `src/Services/Payments/FairBank.Payments.Api/Endpoints/StandingOrderEndpoints.cs`
- `src/FairBank.Web.Shared/Services/FairBankApiClient.cs`

---

## 5. Oprava notifikací (405 / 500)

- **405**: Špatná URL `/notifications/count` → opraveno na `/notifications/unread-count`.
- **500**: Chybějící `GRANT USAGE + ALL ON SCHEMA/TABLES` pro `fairbank_app` na schématech `notifications_service` a `chat_service` → uděleno SQL skriptem.

---

## 6. Oprava trvalého příkazu (500 — DateTime UTC)

Postgres odmítal uložit `TIMESTAMPTZ`, protože `DateTime.Kind` byl `Unspecified`. Opraveno pomocí `DateTime.SpecifyKind(..., DateTimeKind.Utc)` v `CreateStandingOrderCommandHandler`.

---

## 7. Přechod na záložku „Pohyby" z Přehledu

Na hlavní stránce odkaz „Vše" u posledních transakcí navigoval na záložku Platba místo Pohyby. Opraveno změnou `href` na `platby?view=pohyby` a přidáním `[SupplyParameterFromQuery]` v `Payments.razor`.
