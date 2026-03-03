# Opravy buildu a Docker deploymentu

> **Datum:** 2026-03-03
> **Autor:** GitHub Copilot
> **Status:** Vyřešeno

Tento dokument shrnuje opravy provedené za účelem zprovoznění buildu celé solution a nasazení do Dockeru.

## 1. Opravy kompilace (Build Fixes)

Byl opraven nefunkční build solution `FairBank.slnx`, který selhával na několika chybách v Blazor komponentách a API.

### Identity Service API
- **Soubor:** `src/Services/Identity/FairBank.Identity.Api/Program.cs`
- **Chyba:** `DatabaseFacade` neobsahoval definici pro `MigrateAsync`.
- **Oprava:** Přidán chybějící `using Microsoft.EntityFrameworkCore;`.

### Web Profile (Blazor)
- **Soubor:** `src/FairBank.Web.Profile/Pages/Profile.razor`
- **Chyby:**
  - Duplicitní blok `@code` (pravděpodobně po špatném merge).
  - Volání neexistující metody `Auth.LogoutAsync(JS)` (metoda nevyžaduje parametr).
  - Přístup k neexistující vlastnosti `Auth.CurrentUser` (nahrazeno za `Auth.CurrentSession`).
  - Konflikt `IJSRuntime` (odstraněn nepotřebný inject).
- **Oprava:** Kód vyčištěn, odstraněny duplicity, opraveno volání `AuthService`.

### Web Login (Blazor)
- **Soubor:** `src/FairBank.Web/Pages/Login.razor`
- **Chyba:** `IJSRuntime` nebyl nalezen.
- **Oprava:** Přidán `using Microsoft.JSInterop` do `src/FairBank.Web/_Imports.razor`.
- **Chyba:** Metoda `Api.LoginAsync(email, password)` nebyla nalezena na rozhraní.
- **Oprava:** Rozhraní `IFairBankApi` bylo rozšířeno o chybějící přetížení metody.

### Shared Interface
- **Soubor:** `src/FairBank.Web.Shared/Services/IFairBankApi.cs`
- **Změna:** Přidána definice metody:
  ```csharp
  Task<UserResponse?> LoginAsync(string email, string password);
  ```
  Tato metoda již byla implementována v `FairBankApiClient`, ale chyběla v kontraktu.

---

## 2. Opravy Docker Deploymentu

Po zprovoznění buildu bylo nutné opravit nasazení do kontejnerů, které selhávalo při sestavování image pro webovou aplikaci.

### Web Dockerfile
- **Soubor:** `src/FairBank.Web/Dockerfile`
- **Problém:** Docker build selhával, protože chyběly instrukce `COPY` pro projekt `FairBank.Web.Auth`. Tento projekt je závislostí `FairBank.Web`, a proto bez něj nešlo provést `dotnet restore` a `publish`.
- **Oprava:** Přidány chybějící `COPY` příkazy:
  ```dockerfile
  COPY src/FairBank.Web.Auth/FairBank.Web.Auth.csproj            src/FairBank.Web.Auth/
  ...
  COPY src/FairBank.Web.Auth/        src/FairBank.Web.Auth/
  ```

---

## 3. Aktuální stav

- **Solution:** `FairBank.slnx` se úspěšně sestaví (0 Errors).
- **Docker Compose:** Všechny služby běží (`Up`):
  - `fairbank-web` (Port 80)
  - `fairbank-api-gateway` (Port 5000/8080)
  - `fairbank-identity-api` (Internal)
  - `fairbank-accounts-api` (Internal)
  - `fairbank-postgres` (Port 5432)
- **Migrace:** Identitní databáze byla úspěšně migrována (`InitialCreate`).
- **Web App:** Aplikace je dostupná na `http://localhost`.

## Další kroky
- Ověřit funkčnost registrace a přihlášení v běžícím kontejneru.
- Doplnit testy pro nově přidanou metodu v rozhraní `IFairBankApi`.
