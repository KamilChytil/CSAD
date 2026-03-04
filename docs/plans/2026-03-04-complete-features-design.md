# Complete Features Design - Doplnění všech mezer

> **Cíl:** Implementovat 7 chybějících oblastí tak, aby aplikace FairBank pokryla 100% zadání pro všechny 4 role uživatelů.

## 1. Platební karty

### Backend (Accounts service - event-sourced, Marten)

**Nový agregát `Card`:**
- `Id` (Guid), `AccountId` (Guid), `CardNumber` (string, maskované: `**** **** **** 1234`)
- `FullCardNumber` (string, zobrazení jen při vydání)
- `HolderName` (string), `ExpirationDate` (DateTime), `CVV` (string, zobrazení jen při vydání)
- `Type` (enum: Debit, Credit)
- `IsActive` (bool), `IsFrozen` (bool)
- `DailyLimit` (Money?), `MonthlyLimit` (Money?)
- `OnlinePaymentsEnabled` (bool), `ContactlessEnabled` (bool)
- `CreatedAt` (DateTime)

**Domain Events:**
- `CardIssued(CardId, AccountId, CardNumber, HolderName, ExpirationDate, Type, OccurredAt)`
- `CardFrozen(CardId, OccurredAt)`
- `CardUnfrozen(CardId, OccurredAt)`
- `CardLimitSet(CardId, DailyLimit, MonthlyLimit, OccurredAt)`
- `CardSettingsChanged(CardId, OnlinePayments, Contactless, OccurredAt)`
- `CardDeactivated(CardId, OccurredAt)`

**Nový port `ICardEventStore`:**
- `LoadAsync(Guid cardId)`
- `LoadByAccountAsync(Guid accountId)` → `IReadOnlyList<Card>`
- `StartStreamAsync(Card card)`
- `AppendEventsAsync(Card card)`

**API Endpoints:**
- `POST /api/v1/accounts/{id}/cards` → `IssueCardCommand` → 201
- `GET /api/v1/accounts/{id}/cards` → `GetCardsByAccountQuery` → 200
- `POST /api/v1/cards/{id}/freeze` → `FreezeCardCommand` → 204
- `POST /api/v1/cards/{id}/unfreeze` → `UnfreezeCardCommand` → 204
- `PUT /api/v1/cards/{id}/limits` → `SetCardLimitsCommand` → 204
- `PUT /api/v1/cards/{id}/settings` → `UpdateCardSettingsCommand` → 204
- `DELETE /api/v1/cards/{id}` → `DeactivateCardCommand` → 204

**Gateway route:** `/api/v1/cards/{**catch-all}` → `accounts-cluster`

### Frontend (nový modul `FairBank.Web.Cards`)

**Stránka `/karty`:**
- Seznam karet (CardList) - zobrazení maskovaného čísla, typu, statusu
- Detail karty - přepínače: Freeze, Online platby, Contactless
- Nastavení limitů - denní a měsíční limit
- Vydání nové karty - formulář s výběrem typu (Debit/Credit)
- Zrušení karty - s potvrzením

**Navigace:** Přidat "Karty" do SideNav a BottomNav mezi "Platby" a "Spoření".

---

## 2. Frontend dětských účtů

### Backend: Vše existuje
- `POST /api/v1/users/{parentId}/children` - Vytvořit dítě
- `GET /api/v1/users/{parentId}/children` - Seznam dětí
- `GET /api/v1/accounts/{id}/pending` - Pending transakce
- `POST /api/v1/accounts/pending/{id}/approve` - Schválit
- `POST /api/v1/accounts/pending/{id}/reject` - Zamítnout
- `POST /api/v1/accounts/{id}/limits` - Nastavit limity

### Frontend (nová stránka v `FairBank.Web`)

**Stránka `/rodina`:**

**Tab "Děti":**
- Seznam dětských účtů: jméno, email, zůstatek na účtu, status (Aktivní/Neaktivní)
- Tlačítko "Přidat dítě" → formulář CreateChildForm
- Kliknutí na dítě → detail: účty dítěte, poslední transakce

**Tab "Schvalování":**
- Seznam pending transakcí ze všech dětských účtů
- Každá položka: dítě (jméno), částka, popis, datum žádosti
- Tlačítka: Schválit (zelené), Zamítnout (červené s důvodem)

**Tab "Limity":**
- Pro každé dítě: aktuální spending limit, approval threshold
- Formulář pro úpravu limitů

**Formulář CreateChildForm:**
- Jméno, příjmení, email, heslo
- Měna účtu (CZK/EUR/USD/GBP)
- Počáteční spending limit

**Navigace:** Přidat "Rodina" do nav jen pro roli `Client`. Ikona: `users` (VbIcon). Pozice: za "Profil" nebo před "Profil".

---

## 3. Admin správa uživatelů

### Backend (Identity service)

**Nové příkazy:**
- `GetAllUsersQuery(Page, PageSize, RoleFilter?, SearchTerm?)` → stránkovaný výsledek
- `UpdateUserRoleCommand(UserId, NewRole)` → změna role
- `DeactivateUserCommand(UserId)` → nastavení `IsActive = false`
- `ActivateUserCommand(UserId)` → nastavení `IsActive = true`
- `DeleteUserCommand(UserId)` → soft delete (`User.SoftDelete()`)

**Nové DTO:**
- `PagedUsersResponse(Items, TotalCount, Page, PageSize)`

**Nové endpointy:**
- `GET /api/v1/users?page=1&pageSize=20&role=Client&search=novak` → `GetAllUsersQuery` → 200
- `PUT /api/v1/users/{id}/role` → `UpdateUserRoleCommand` → 204
- `POST /api/v1/users/{id}/deactivate` → `DeactivateUserCommand` → 204
- `POST /api/v1/users/{id}/activate` → `ActivateUserCommand` → 204
- `DELETE /api/v1/users/{id}` → `DeleteUserCommand` → 204

### Frontend (v existující Admin stránce)

**Rozšíření `/admin` stránky:**
- Nahradíme placeholder "User management will be added in next version"
- Tabulka uživatelů: Jméno, Email, Role, Status, Datum registrace, Akce
- Filtrování: dropdown pro roli + textový search
- Stránkování: 20 uživatelů na stránku
- Akce: Role dropdown, Activate/Deactivate toggle, Delete button (s potvrzením)
- Barevné indikátory: zelená (Active), červená (Inactive), šedá (Deleted)

---

## 4. Bankéřský dashboard

### Backend (Chat service - nový endpoint)

**Nový endpoint:**
- `GET /api/v1/chat/conversations/banker/{bankerId}/clients` → seznam unikátních klientů přiřazených bankéři

**Nové DTO:**
- `BankerClientDto(ClientId, ClientName, ClientEmail, ActiveChatsCount, LastActivity)`

### Frontend (rozšíření Management.razor)

**Stávající tab "Žádosti"** - beze změn (schvalování produktů)

**Nový tab "Klienti":**
- Seznam klientů přiřazených bankéři (z chatových konverzací)
- Karta klienta: jméno, email, počet aktivních chatů, poslední aktivita
- Kliknutí na klienta → detail: účty klienta, jejich zůstatky, poslední transakce, aktivní chaty

**Nový tab "Přehled":**
- Statistiky: celkem klientů, aktivních chatů, pending žádostí
- Rychlé akce: otevřít nepřiřazený chat, přejít na žádosti

---

## 5. Spoření backend

### Backend (Accounts service - event-sourced, Marten)

**Nový agregát `SavingsGoal`:**
- `Id` (Guid), `AccountId` (Guid)
- `Name` (string), `Description` (string?)
- `TargetAmount` (Money), `CurrentAmount` (Money)
- `Currency` (Currency enum)
- `IsCompleted` (bool), `CreatedAt` (DateTime), `CompletedAt` (DateTime?)

**Domain Events:**
- `SavingsGoalCreated(GoalId, AccountId, Name, TargetAmount, Currency, OccurredAt)`
- `SavingsDeposited(GoalId, Amount, Currency, OccurredAt)`
- `SavingsWithdrawn(GoalId, Amount, Currency, OccurredAt)`
- `SavingsGoalCompleted(GoalId, OccurredAt)`

**Nový agregát `SavingsRule`:**
- `Id` (Guid), `AccountId` (Guid)
- `Name` (string), `Description` (string?)
- `Type` (enum: RoundUp, FixedWeekly, FixedMonthly, PercentageOfIncome)
- `Amount` (decimal), `IsEnabled` (bool), `CreatedAt` (DateTime)

**Domain Events:**
- `SavingsRuleCreated(RuleId, AccountId, Name, Type, Amount, OccurredAt)`
- `SavingsRuleToggled(RuleId, IsEnabled, OccurredAt)`

**Porty:**
- `ISavingsGoalEventStore` - CRUD pro spořicí cíle
- `ISavingsRuleEventStore` - CRUD pro pravidla

**API Endpoints:**
- `POST /api/v1/accounts/{id}/savings-goals` → 201
- `GET /api/v1/accounts/{id}/savings-goals` → 200
- `POST /api/v1/savings-goals/{id}/deposit` → 204
- `POST /api/v1/savings-goals/{id}/withdraw` → 204
- `DELETE /api/v1/savings-goals/{id}` → 204
- `POST /api/v1/accounts/{id}/savings-rules` → 201
- `GET /api/v1/accounts/{id}/savings-rules` → 200
- `PUT /api/v1/savings-rules/{id}/toggle` → 204

**Gateway routes:**
- `/api/v1/savings-goals/{**catch-all}` → `accounts-cluster`
- `/api/v1/savings-rules/{**catch-all}` → `accounts-cluster`

### Frontend (úprava existujícího Savings.razor)

- Nahradit demo data reálnými API voláními
- Formulář "Nový cíl" - název, popis, cílová částka, měna
- Progress bar s reálnými daty (currentAmount / targetAmount)
- Tlačítka: Vložit do cíle, Vybrat z cíle, Smazat cíl
- Seznam pravidel s toggle přepínači (reálné API)
- Formulář "Nové pravidlo" - název, typ, částka

---

## 6. Investice backend

### Backend (Accounts service - event-sourced, Marten)

**Nový agregát `Investment`:**
- `Id` (Guid), `AccountId` (Guid)
- `Name` (string), `Type` (enum: Stock, Bond, Crypto, Fund)
- `InvestedAmount` (Money), `CurrentValue` (Money)
- `Units` (decimal), `PricePerUnit` (decimal)
- `Currency` (Currency enum)
- `IsActive` (bool), `CreatedAt` (DateTime), `SoldAt` (DateTime?)

**Domain Events:**
- `InvestmentCreated(InvestmentId, AccountId, Name, Type, InvestedAmount, Units, PricePerUnit, OccurredAt)`
- `InvestmentValueUpdated(InvestmentId, NewValue, NewPricePerUnit, OccurredAt)`
- `InvestmentSold(InvestmentId, SoldAmount, OccurredAt)`

**Port `IInvestmentEventStore`:**
- `LoadAsync(Guid investmentId)`
- `LoadByAccountAsync(Guid accountId)` → `IReadOnlyList<Investment>`
- `StartStreamAsync(Investment investment)`
- `AppendEventsAsync(Investment investment)`

**API Endpoints:**
- `POST /api/v1/accounts/{id}/investments` → `CreateInvestmentCommand` → 201
- `GET /api/v1/accounts/{id}/investments` → `GetInvestmentsByAccountQuery` → 200
- `GET /api/v1/investments/{id}` → `GetInvestmentByIdQuery` → 200
- `PUT /api/v1/investments/{id}/value` → `UpdateInvestmentValueCommand` → 204
- `POST /api/v1/investments/{id}/sell` → `SellInvestmentCommand` → 204

**Gateway route:** `/api/v1/investments/{**catch-all}` → `accounts-cluster`

**Seeder:** Demo investice pro seeded účty (Akciový fond, Dluhopisový fond, Kryptoměny).

### Frontend (úprava existujícího Investments.razor)

- Nahradit demo data reálnými API voláními
- Portfolio přehled: celková hodnota, změna %
- Karta investice: název, typ, investováno vs aktuální hodnota, změna %
- Formulář "Nová investice" - název, typ, částka, počet podílů
- Tlačítko "Prodat" s potvrzením
- Sparkline chart zachovat (s reálnými daty nebo generovanými)

---

## 7. Editace profilu

### Backend (Identity service)

**Nové příkazy:**
- `ChangeEmailCommand(UserId, NewEmail)` → ověří unikátnost, aktualizuje email
- `ChangePasswordCommand(UserId, CurrentPassword, NewPassword)` → ověří staré heslo BCryptem, nastaví nové

**Nové endpointy:**
- `PUT /api/v1/users/{id}/email` → `ChangeEmailCommand` → 204
- `PUT /api/v1/users/{id}/password` → `ChangePasswordCommand` → 204

**Validace:**
- Email: formát, unikátnost (ne stejný jako jiný uživatel)
- Heslo: min 8 znaků, velké písmeno, malé písmeno, číslice, speciální znak
- Staré heslo: BCrypt verify

### Frontend (úprava Profile.razor)

- Tlačítko "Změnit email" → inline formulář s novým emailem
- Tlačítko "Změnit heslo" → formulář: staré heslo, nové heslo, potvrzení nového hesla
- Po úspěšné změně emailu → aktualizovat AuthSession v localStorage
- Validace na frontendu (stejná pravidla jako registrace)

---

## Přehled změn v Gateway

Nové YARP routes v `appsettings.json`:

| Route | Pattern | Cluster |
|-------|---------|---------|
| `cards-route` | `/api/v1/cards/{**catch-all}` | `accounts-cluster` |
| `savings-goals-route` | `/api/v1/savings-goals/{**catch-all}` | `accounts-cluster` |
| `savings-rules-route` | `/api/v1/savings-rules/{**catch-all}` | `accounts-cluster` |
| `investments-route` | `/api/v1/investments/{**catch-all}` | `accounts-cluster` |

Existující routes pokryjí `/api/v1/users/*` (admin + profil) a `/api/v1/accounts/*` (účet-level endpointy pro karty/spoření/investice).

---

## Přehled nových frontend modulů

| Modul | Stránka | Nový/Úprava |
|-------|---------|-------------|
| `FairBank.Web.Cards` | `/karty` | NOVÝ |
| `FairBank.Web` | `/rodina` | NOVÁ stránka |
| `FairBank.Web` / `FairBank.Admin.Web` | `/admin` | ÚPRAVA |
| `FairBank.Web.Products` | `/sprava` | ÚPRAVA (nové taby) |
| `FairBank.Web.Savings` | `/sporeni` | ÚPRAVA (reálná data) |
| `FairBank.Web.Investments` | `/investice` | ÚPRAVA (reálná data) |
| `FairBank.Web.Profile` | `/profil` | ÚPRAVA (editace) |
| `FairBank.Web` | SideNav + BottomNav | ÚPRAVA (nové odkazy) |

---

## Navigace po změnách

**Všechny role:**
- Přehled, Platby, **Karty** (nové), Spoření, Investice, Kurzy, Zprávy, Profil

**Client (navíc):**
- Produkty, **Rodina** (nové)

**Child:**
- Bez Produktů, bez Rodiny

**Banker (navíc):**
- Produkty, Správa (rozšířená o taby Klienti + Přehled)

**Admin (navíc):**
- Produkty, Admin (rozšířená o správu uživatelů)
