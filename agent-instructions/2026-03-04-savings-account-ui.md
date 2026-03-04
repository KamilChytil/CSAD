# 2026-03-04 — Spořicí účet, rozdělení zůstatků a záložka Spoření

## Přehled změn

Tato session přidala plnou podporu spořicího účtu jako oddělené entity od běžného účtu — viditelnou na hlavní stránce i v záložce Spoření.

---

## 1. Záložka Spoření — karta spořicího účtu

### Co se udělalo
Na stránce `/sporeni` se nově zobrazuje karta **„SPOŘICÍ ÚČET"** vždy jako první prvek.

**Varianty podle stavu:**

**A) Spořicí účet existuje:**
- Zobrazí aktuální zůstatek, číslo účtu a zelený badge „Aktivní"

**B) Spořicí účet neexistuje:**
- Zobrazí informaci, že uživatel spořicí účet nemá
- Tlačítko **„Zažádat o spořicí účet"** — kliknutím se účet okamžitě vytvoří přes `POST /api/v1/accounts` s `AccountType = 1`
- Po úspěšném vytvoření se zobrazí potvrzení a karta se přepne na variantu A

**Dotčené soubory:**
- `src/FairBank.Web.Savings/Pages/Savings.razor`
- `src/FairBank.Web.Shared/Services/IFairBankApi.cs` — přidána metoda `CreateSavingsAccountAsync`
- `src/FairBank.Web.Shared/Services/FairBankApiClient.cs` — implementace `CreateSavingsAccountAsync`

---

## 2. Hlavní stránka — rozdělení zůstatků

### Problém
Na hlavní stránce (`/prehled`) se zobrazoval jediný `BalanceCard` se součtem všech účtů. Při přidání spořicího účtu byl jeho zůstatek přičítán k běžnému účtu bez rozlišení.

### Oprava
- Hero `BalanceCard` nyní zobrazuje pouze **běžný účet** (label „BĚŽNÝ ÚČET", číslo běžného účtu, pouze jeho zůstatek)
- Pod ním se podmíněně zobrazí samostatná karta **„SPOŘICÍ ÚČET"** — pokud uživatel spořicí účet má — se zůstatkem, číslem účtu a odkazem „Spravovat" do záložky Spoření

**Logika výběru účtu:**
```csharp
_account = accounts.FirstOrDefault(a => a.AccountType == "Checking") ?? accounts[0];
_savingsAccount = accounts.FirstOrDefault(a => a.AccountType == "Savings");
_checkingBalance = _account.Balance;
```

**Dotčené soubory:**
- `src/FairBank.Web.Overview/Pages/Overview.razor`

---

## 3. Záložka Převod — rozlišení typů účtů

### Co bylo v předchozí session
Oba dropdowny v převodu zobrazovaly jen číslo účtu. Bylo tedy možné vybrat v obou kolonkách tentýž účet (nebo nevědět, který je spořicí).

### Oprava (session předtím)
Každá položka v dropdownu nyní zobrazuje `TypeLabel` před číslem:
- `Běžný účet – 000000-0000000001/8888 (15 000 Kč)`
- `Spořicí účet – 000000-0000000002/8888 (3 500 Kč)`

**Logika `TypeLabel`:**
```csharp
public string TypeLabel => AccountType == "Savings" ? "Spořicí účet" : "Běžný účet";
```

---

## 4. Automatické zakládání spořicího účtu při registraci

Při registraci i vytváření dětského účtu se nově automaticky vytvářejí **oba typy účtů**:
1. Běžný účet (`AccountType = 0`)
2. Spořicí účet (`AccountType = 1`)

Stávajícím uživatelům byl spořicí účet doplněn skriptem `docker/provision_savings_accounts.ps1`.

---

## 5. AccountType v celém backendu

### Nová entita
- Enum `AccountType` (`Checking = 0`, `Savings = 1`) v `FairBank.Accounts.Domain.Enums`
- Propagován přes: domain → event (`AccountCreated`) → command (`CreateAccountCommand`) → všechny handlery → `AccountResponse` DTO (backend i frontend model)

### Frontendový model
```csharp
public sealed record AccountResponse(
    ...
    string AccountType = "Checking")
{
    public string TypeLabel => AccountType == "Savings" ? "Spořicí účet" : "Běžný účet";
}
```

---

## Jak to funguje dohromady

```
Registrace uživatele
  └── identity-api POST /api/v1/users/register
       ├── accounts-api POST /api/v1/accounts  { AccountType: 0 }  → Běžný účet
       └── accounts-api POST /api/v1/accounts  { AccountType: 1 }  → Spořicí účet

Přehled (/)
  ├── BalanceCard: Běžný účet (jen jeho zůstatek)
  └── ContentCard: Spořicí účet (jen jeho zůstatek) — pokud existuje

Spoření (/sporeni)
  ├── Karta Spořicí účet (aktivní) — NEBO —
  │   Karta "Zažádat o spořicí účet" → volá CreateSavingsAccountAsync
  └── Spořicí cíle a pravidla (vázány na spořicí účet)

Převod (/platby → Převod)
  ├── Z účtu: [Běžný účet – číslo (zůstatek)]
  └── Na účet: [Spořicí účet – číslo (zůstatek)]  (filtruje vybraný Z účtu)
```
