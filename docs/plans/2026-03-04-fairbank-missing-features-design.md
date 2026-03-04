# FairBank – Design: Doplnění chybějících funkcí

**Datum:** 2026-03-04
**Stav:** Schváleno
**Rozsah:** Produkční kvalita, feature-by-feature přístup

---

## Kontext

Analýza projektu FairBank oproti 10 požadovaným funkcím aktéra "Uživatel (klient)" odhalila celkové pokrytí ~41%. Tento dokument popisuje design doplnění všech chybějících funkcí na 100%.

### Výchozí stav

| # | Požadavek | Stav před | Cíl |
|---|-----------|-----------|-----|
| 1 | Registrace do systému | ~40% | 100% |
| 2 | Ověření identity | ~10% | 100% |
| 3 | Správa bankovního účtu | ~85% | 100% |
| 4 | Provádění plateb | ~90% | 100% |
| 5 | Správa platebních karet | 0% | 100% |
| 6 | Finanční a bezpečnostní limity | ~50% | 100% |
| 7 | Statistiky a historie transakcí | ~35% | 100% |
| 8 | Příjem notifikací | ~5% | 100% |
| 9 | Správa přihlášených zařízení | ~15% | 100% |
| 10 | Komunikace klient–bankéř | ~80% | 100% |

---

## Sekce 1: Identita & Bezpečnost

### 1. Registrace – doplnění na 100%

**Problém:** Backend přijímá jen 5 polí (jméno, příjmení, email, heslo, role). Frontend sbírá KYC data (rodné číslo, telefon, adresa, datum narození), ale ta se nikam neukládají.

**Řešení:**
- Rozšířit doménový model `User` o pole: `PersonalIdNumber`, `DateOfBirth`, `PhoneNumber`, `Street`, `City`, `ZipCode`, `Country`, `AgreedToTermsAt`
- Přidat Value Object `Address` a `PhoneNumber` s validací
- Rozšířit `RegisterUserCommand` o všechna pole z frontendu
- EF Core migrace pro nové sloupce
- Validace rodného čísla (český checksum algoritmus)
- Endpoint pro email verifikaci: `POST /api/v1/users/verify-email` s tokenem
- Endpoint pro opětovné odeslání: `POST /api/v1/users/resend-verification`
- Email sending service (SMTP abstrakce)

### 2. Ověření identity – z 10% na 100%

**Problém:** Žádné 2FA, email verifikace, reset hesla.

**Řešení:**

**Email verifikace:**
- Při registraci vygenerovat token, odeslat email, `IsEmailVerified` flag na User entitě
- Login blokován dokud není ověřen

**2FA (TOTP):**
- Nová entita `TwoFactorAuth` – setup endpoint (generuje QR kód pro Authenticator app), verify endpoint, enable/disable
- Při loginu pokud 2FA aktivní → vyžadovat kód
- Endpointy: `/api/v1/users/2fa/setup`, `/verify`, `/enable`, `/disable`

**Reset hesla:**
- `POST /api/v1/users/forgot-password` (generuje token, posílá email)
- `POST /api/v1/users/reset-password` (validuje token, nastaví nové heslo)

**Změna hesla:**
- `POST /api/v1/users/change-password` (vyžaduje staré heslo)

### 9. Správa přihlášených zařízení – z 15% na 100%

**Problém:** Single-session enforcement bez device tracking.

**Řešení:**
- Nová entita `UserDevice`: `Id`, `UserId`, `DeviceName`, `DeviceType`, `Browser`, `OperatingSystem`, `IpAddress`, `LastActiveAt`, `SessionId`, `IsTrusted`, `CreatedAt`
- Při každém loginu: detekce User-Agent → uložení/aktualizace device záznamu
- Podpora více aktivních sessions (rozšíření z single-session na multi-device)
- Endpointy:
  - `GET /api/v1/users/devices` – seznam zařízení
  - `DELETE /api/v1/users/devices/{id}` – vzdálené odhlášení
  - `PUT /api/v1/users/devices/{id}/trust` – označit jako důvěryhodné
- UI v profilu: Seznam zařízení s možností odhlásit/označit důvěryhodné
- Notifikace při přihlášení z nového zařízení

---

## Sekce 2: Bankovní operace

### 3. Správa bankovního účtu – z 85% na 100%

**Problém:** Žádné uzavření účtu, editace, přehled všech účtů.

**Řešení:**
- `CloseAccountCommand` – validace nulového zůstatku, `IsActive = false`, emituje `AccountClosed` event. Endpoint: `POST /api/v1/accounts/{id}/close`
- `RenameAccountCommand` – uživatelský alias. Nové pole `Alias`. Endpoint: `PUT /api/v1/accounts/{id}/alias`
- Dashboard multi-účet: Overview zobrazí všechny účty s přepínačem, celkový zůstatek
- Detail účtu: Samostatná stránka s kompletním přehledem

### 4. Provádění plateb – z 90% na 100%

**Problém:** QR platby jen jako tlačítko bez implementace.

**Řešení:**
- QR příjem: Generování QR kódu dle SPAYD (Short Payment Descriptor). Endpoint: `GET /api/v1/accounts/{id}/qr-code?amount={}&message={}`
- QR odeslání: JS interop pro čtení QR z kamery/upload, parsování SPAYD, předvyplnění formuláře
- Validace formátu českého čísla účtu v `SendPaymentCommand`

### 5. Platební karty – z 0% na 100% (NOVÁ SLUŽBA)

**Nový microservice: `FairBank.Cards`**

**Domain model:**
```
Card (AggregateRoot)
├── Id (Guid)
├── AccountId (Guid)
├── UserId (Guid)
├── CardNumber (ValueObject, maskované)
├── CardholderName (string)
├── ExpirationDate (DateOnly)
├── CardType (enum: Debit, Credit)
├── CardBrand (enum: Visa, Mastercard)
├── Status (enum: Active, Blocked, Expired, Cancelled)
├── DailyLimit (Money)
├── MonthlyLimit (Money)
├── OnlinePaymentsEnabled (bool)
├── ContactlessEnabled (bool)
├── CreatedAt, UpdatedAt
```

**Operace:**
- `IssueCardCommand` – vydání nové karty (generuje číslo, CVV hash, expirace +3 roky)
- `BlockCardCommand` / `UnblockCardCommand`
- `SetCardLimitsCommand` – denní/měsíční limity, online/contactless toggle
- `CancelCardCommand` – trvalé zrušení
- `RenewCardCommand` – obnova expirované karty
- `SetPinCommand` / `ChangePinCommand` (hashovaný PIN)

**API endpointy:**
```
POST   /api/v1/cards/                     → Vydat kartu
GET    /api/v1/cards/account/{accountId}  → Karty k účtu
GET    /api/v1/cards/{id}                 → Detail karty
PUT    /api/v1/cards/{id}/block           → Blokovat
PUT    /api/v1/cards/{id}/unblock         → Odblokovat
PUT    /api/v1/cards/{id}/limits          → Nastavit limity
PUT    /api/v1/cards/{id}/settings        → Online/contactless
DELETE /api/v1/cards/{id}                 → Zrušit kartu
POST   /api/v1/cards/{id}/renew           → Obnovit kartu
PUT    /api/v1/cards/{id}/pin             → Nastavit/změnit PIN
```

**Bezpečnost:**
- CVV nikdy v odpovědích API (jen hash v DB)
- Celé číslo karty zobrazeno jen při vydání, pak maskované
- PIN hashován (BCrypt)

**UI (FairBank.Web.Cards):**
- Vizuální karta (číslo, jméno, expirace, typ)
- Přehled všech karet
- Správa limitů (slidery/inputy)
- Toggle online plateb a contactless
- Blokace/zrušení s potvrzovacím dialogem

---

## Sekce 3: Limity & Monitoring

### 6. Finanční a bezpečnostní limity – z 50% na 100%

**Problém:** Jen SpendingLimit a ApprovalThreshold. Chybí granulární limity, bezpečnostní limity, UI.

**Řešení:**

**Rozšíření Account entity – AccountLimits (Value Object):**
- `DailyTransactionLimit` (Money) – max denní objem
- `MonthlyTransactionLimit` (Money) – max měsíční objem
- `SingleTransactionLimit` (Money) – max jedna platba
- `DailyTransactionCount` (int) – max počet plateb za den
- `OnlinePaymentLimit` (Money) – limit pro online platby

**Nové commands:**
- `SetAccountLimitsCommand` – nastavení všech limitů. Endpoint: `PUT /api/v1/accounts/{id}/limits`
- `GetAccountLimitsQuery` – přehled limitů a využití. Endpoint: `GET /api/v1/accounts/{id}/limits`

**Enforcement:**
- Při každé platbě ověřit všechny limity (single, denní, měsíční, počet)
- Odmítnutí s konkrétní chybou ("Překročen denní limit 50 000 Kč")

**Bezpečnostní limity na User entitě – SecuritySettings:**
- `RequireApprovalAbove` (Money)
- `AllowInternationalPayments` (bool)
- `NightTransactionsEnabled` (bool) – platby 23:00–06:00
- Endpoint: `PUT /api/v1/users/{id}/security-settings`

**UI:**
- Karta "Limity" se slidery a inputy
- Vizuální indikátor využití (progress bar)
- Toggle pro bezpečnostní nastavení

### 7. Statistiky a historie transakcí – z 35% na 100%

**Problém:** Max 50 záznamů, žádné filtrování/analytika/export.

**Řešení:**

**Pokročilý query:**
`GET /api/v1/payments/account/{id}?dateFrom=&dateTo=&minAmount=&maxAmount=&type=&status=&search=&page=&pageSize=&sortBy=&sortDir=`

**Statistiky:**
`GET /api/v1/payments/account/{id}/statistics?period=monthly&dateFrom=&dateTo=`
- Celkové příjmy/výdaje, počet transakcí, průměr, největší výdaj
- Kategorizace: Housing, Food, Transport, Entertainment, Health, Shopping, Savings, Salary, Other
- Měsíční trend (příjmy vs. výdaje po měsících)

**Kategorizace transakcí:**
- Nové pole `Category` na Payment (enum)
- Automatická kategorizace podle klíčových slov v description (rule-based)
- Ruční přepis: `PUT /api/v1/payments/{id}/category`

**Export:**
- `GET /api/v1/payments/account/{id}/export?format=csv&dateFrom=&dateTo=`
- `GET /api/v1/payments/account/{id}/export?format=pdf&dateFrom=&dateTo=`

**UI:**
- Dashboard: Koláčový graf výdajů, sloupcový graf příjmy vs. výdaje
- Historie: Tabulka s filtrováním, řazením, stránkováním, vyhledáváním
- Detail transakce s možností změny kategorie
- Export tlačítko (CSV/PDF)

---

## Sekce 4: Komunikace & Notifikace

### 8. Příjem notifikací – z 5% na 100% (NOVÁ SLUŽBA)

**Nový microservice: `FairBank.Notifications`**

**Domain model:**
```
Notification (AggregateRoot)
├── Id (Guid)
├── UserId (Guid)
├── Title (string)
├── Message (string)
├── Type (enum: Transaction, Security, Card, Limit, System, Chat)
├── Priority (enum: Low, Normal, High, Critical)
├── Channel (enum: InApp, Email, Push)
├── Status (enum: Pending, Sent, Read, Failed)
├── RelatedEntityType (string?)
├── RelatedEntityId (Guid?)
├── CreatedAt, ReadAt, SentAt

NotificationPreference (Entity)
├── UserId (Guid)
├── TransactionNotifications (bool, default true)
├── SecurityNotifications (bool, default true, nelze vypnout)
├── CardNotifications (bool, default true)
├── LimitNotifications (bool, default true)
├── ChatNotifications (bool, default true)
├── EmailNotificationsEnabled (bool, default true)
├── PushNotificationsEnabled (bool, default false)
```

**Real-time – SignalR `NotificationHub`:**
- Klient se připojí při startu, přijímá notifikace v reálném čase
- Metody: `OnNotificationReceived`, `OnNotificationCountUpdated`

**Triggery notifikací:**
| Událost | Typ | Priorita |
|---------|-----|----------|
| Příchozí/odchozí platba | Transaction | Normal |
| Platba odmítnuta | Transaction | High |
| Přihlášení z nového zařízení | Security | Critical |
| Neúspěšné pokusy o přihlášení | Security | High |
| Karta zablokována | Card | High |
| Expirace karty za 30 dní | Card | Normal |
| Limit dosažen 80% | Limit | Normal |
| Nová zpráva od bankéře | Chat | Normal |
| Schválení transakce vyžadováno | Transaction | High |

**Inter-service komunikace:**
- HTTP volání z command handlerů na Notification API

**Email notifikace:**
- `IEmailSender` interface, SMTP implementace
- HTML šablony (Razor templating)
- Rate limiting: max 1 email za typ za 5 minut

**API endpointy:**
```
GET    /api/v1/notifications/                    → Seznam (stránkovaný)
GET    /api/v1/notifications/unread-count        → Počet nepřečtených
PUT    /api/v1/notifications/{id}/read           → Označit přečtené
PUT    /api/v1/notifications/read-all            → Označit vše
DELETE /api/v1/notifications/{id}                → Smazat
GET    /api/v1/notifications/preferences         → Získat preference
PUT    /api/v1/notifications/preferences         → Uložit preference
```

**UI:**
- Notifikační ikona v hlavičce s badge
- Dropdown panel s posledními notifikacemi
- Stránka notifikací s filtrem podle typu
- Nastavení v profilu – toggle pro každý typ a kanál

### 10. Komunikace klient–bankéř – z 80% na 100%

**Problém:** Chybí přílohy, indikátor psaní, potvrzení přečtení, vyhledávání.

**Přílohy v chatu:**
- Entita `ChatAttachment`: `Id`, `MessageId`, `FileName`, `ContentType`, `FileSize`, `StoragePath`
- Max 10 MB, povolené typy: PDF, PNG, JPG, DOCX
- Upload: `POST /api/v1/chat/messages/{id}/attachments`
- Stažení: `GET /api/v1/chat/attachments/{id}/download`
- Úložiště: `/data/chat-attachments/` (docker volume)

**Indikátor psaní:**
- SignalR: `StartTyping(conversationId)`, `StopTyping(conversationId)`
- Hub broadcastuje `UserTyping(userId, userName)`
- Debounce 2s

**Potvrzení přečtení:**
- Pole `ReadAt` na `ChatMessage`
- SignalR: `MarkAsRead(messageId)` → broadcastuje `MessageRead`
- UI: ✓✓ přečtené, ✓ doručené

**Vyhledávání:**
- `GET /api/v1/chat/conversations/{id}/messages/search?query=`
- Fulltext search, zvýraznění výrazů

---

## Architektonické poznámky

### Nové microservices
- **FairBank.Cards** – Clean Architecture (Domain/Application/Infrastructure/Api), Event Sourcing (Marten)
- **FairBank.Notifications** – Clean Architecture, PostgreSQL (EF Core)

### Sdílené vzory
- CQRS s MediatR
- Repository pattern
- Value Objects pro doménovou validaci
- Docker Compose rozšíření pro nové služby

### Bezpečnost
- BCrypt pro PINy a hesla
- TOTP dle RFC 6238 pro 2FA
- Tokeny s expirací pro email verifikaci a reset hesla
- Rate limiting na citlivých endpointech
- Maskování citlivých dat (číslo karty, CVV)
