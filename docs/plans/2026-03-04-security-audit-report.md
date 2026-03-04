# FairBank — Bezpečnostní Audit

> **Datum:** 2026-03-04
> **Metoda:** Checklist scoring + matice hrozeb
> **Branch:** `security` (totožná s `main` k datu auditu)
> **Analyzovaný kód:** Celý `src/`, `docker-compose.yml`, `docker/postgres/`, `docs/`

---

## Souhrn

Celkový vážený průměr bezpečnostního pokrytí: **~24%**

| # | Kategorie | Skóre | Závažnost mezer |
|---|-----------|-------|-----------------|
| 1 | Šifrování dat (at-rest & in-transit) | **15%** | Vysoká |
| 2 | Secure komunikace FE ↔ BE ↔ DB | **25%** | Vysoká |
| 3 | RBAC (Role-Based Access Control) | **20%** | Kritická |
| 4 | Logování auditních událostí | **20%** | Vysoká |
| 5 | Rate limiting | **18%** | Vysoká |
| 6 | Anti-bot ochrana | **0%** | Střední |
| 7 | CORS ochrana | **20%** | Střední |
| 8 | DB redundance | **35%** | Střední |
| 9 | Soft delete účtů | **45%** | Střední |
| 10 | Scénáře výpadku (resilience) | **25%** | Vysoká |
| 11 | Ochrana před zranitelnostmi | **~40%** avg | SQL injection výborný, BOLA kritický |

**Top 3 kritické priority:**
1. **BOLA (5%)** — žádný endpoint neověřuje vlastnictví zdroje
2. **RBAC enforcement (20%)** — role existují v doméně, ale žádný endpoint nepožaduje autorizaci
3. **Šifrování (15%)** — žádný HTTPS, žádný DB SSL, citlivá data plaintext

---

## 1. Šifrování dat (at-rest & in-transit) — 15%

### Co existuje

- **BCrypt password hashing** (work factor 12) — hesla jsou správně hashována, nikdy ukládána plaintext
  - `src/Services/Identity/FairBank.Identity.Application/Users/Commands/RegisterUser/RegisterUserCommandHandler.cs`
  - `src/Services/Identity/FairBank.Identity.Application/Users/Commands/LoginUser/LoginUserCommandHandler.cs`
- **PostgreSQL data checksums** (`--data-checksums` v docker-compose) — detekce korupce dat
- **SMTP s EnableSsl** — emailový sender používá TLS
  - `src/Services/Identity/FairBank.Identity.Infrastructure/Email/SmtpEmailSender.cs:67`
- **Cryptographically random tokeny** — email verification a password reset tokeny generovány přes `RandomNumberGenerator.GetBytes(32)`
  - `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs:214,237`

### Co chybí

- **TLS/HTTPS termination** — nginx servíruje na portu 80 (HTTP), žádný certifikát, žádné HTTPS. Design doc zmiňuje "self-signed cert pro dev", ale není implementováno
- **Šifrování interní Docker sítě** — backend network je `internal: true` (izolovaná), ale komunikace mezi službami je plain HTTP
- **PostgreSQL SSL** — connection stringy neobsahují `SslMode=Require`, komunikace app→DB je nešifrovaná
- **Encryption at-rest** — žádné šifrování datových volumů, žádný TDE (Transparent Data Encryption)
- **Šifrování citlivých dat v DB** — PersonalIdNumber, Address, PhoneNumber jsou uloženy plaintext v `identity_service.users`

---

## 2. Secure komunikace frontend ↔ backend ↔ databáze — 25%

### Co existuje

- **YARP API Gateway** — jediný vstupní bod do backendu, frontend nikdy nekomunikuje přímo se službami
  - `src/FairBank.ApiGateway/Program.cs`
  - `src/FairBank.ApiGateway/appsettings.json` — 7 clusterů routovaných přes YARP
- **Docker network isolation** — `backend` síť je `internal: true`, pouze `web-app` a `api-gateway` mají přístup ven (frontend síť)
- **Pouze port 80 vystavený** — žádná backend služba nemá `ports`, všechny jen `expose`
- **Session token** — frontend posílá Base64-encoded `userId:sessionId` v Authorization headeru
  - `src/Services/Identity/FairBank.Identity.Application/Helpers/SessionTokenHelper.cs`
- **Server-side session validace** — `User.IsSessionValid()` ověřuje sessionId + server-enforced expiry
  - `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs:204-208`

### Co chybí

- **HTTPS** — vše běží na HTTP (port 80), žádný TLS certifikát na nginx
- **JWT** — session token je `Base64(userId:sessionId)`, není podepsaný (žádný HMAC/RSA). Kdokoliv může token zfalšovat — stačí znát userId a uhodnout sessionId. Design doc plánoval JWT s HS256, neimplementováno
- **DB connection encryption** — connection stringy bez `SslMode=Require`
- **mTLS mezi službami** — interní komunikace je plain HTTP
- **API Gateway nevaliduje tokeny** — gateway jen proxyuje, žádné JWT middleware, žádné ověření identity na gateway úrovni

---

## 3. RBAC (Role-Based Access Control) — 20%

### Co existuje

- **4 role v doméně** — `Client`, `Child`, `Banker`, `Admin` (enum `UserRole`)
  - `src/Services/Identity/FairBank.Identity.Domain/Enums/UserRole.cs`
- **Role uložena na User entitě** — `User.Role` se posílá ve frontend session
- **Frontend role-based UI** — frontend rozlišuje role (Banker tools, Admin panel, Parent/Child views)
- **Parent-Child vztah** — `ParentId` self-reference FK, validace že pouze `Client` může být rodič
  - `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs:101-111`
- **Spending limits per child** — rodič nastavuje limity na účtu dítěte

### Co chybí

- **Žádné `.RequireAuthorization()` na endpointech** — všechny API endpointy jsou otevřené, bez jakékoliv autorizace
- **Žádné RBAC middleware** — gateway neprovádí žádnou kontrolu rolí
- **Žádné policy-based authorization** — design doc plánoval `ClientOnly`, `BankerOnly`, `AdminOnly`, `ParentOfChild` policies — nic z toho neexistuje
- **Žádná ochrana BOLA** — endpointy nekontrolují zda přihlášený uživatel je vlastníkem zdroje (účtu, platby). Kdokoliv může přistoupit k datům kohokoliv přes ID v URL
- **Banker/Admin funkce nechráněny** — chat assign, close conversation, admin seeder — vše přístupné bez ověření role

---

## 4. Logování auditních událostí — 20%

### Co existuje

- **Serilog → Console + Kafka pipeline** — 3 hlavní služby (Identity, Accounts, Gateway) posílají logy do Kafka
  - `src/FairBank.SharedKernel/Logging/ConfluentKafkaSink.cs`
- **`UseSerilogRequestLogging()`** — na 6 z 8 služeb, loguje HTTP method, path, status code, elapsed time
  - Identity, Accounts, Payments, Cards, Products, ApiGateway
- **Admin Web + SQLite viewer** — `KafkaLogConsumerService` konzumuje z Kafka, ukládá do SQLite
  - `src/FairBank.Admin.Web/Services/KafkaLogConsumerService.cs`
  - Admin endpoint `/api/logs` s filtrováním dle search/level/service
- **Email Sender loguje send success/failure**
  - `src/Services/Identity/FairBank.Identity.Infrastructure/Email/SmtpEmailSender.cs:80,84`

### Co chybí

- **Žádný `AuditLog` entity/tabulka/služba** — neexistuje nikde v kódu
- **Žádné security event logging** — žádný handler v Identity nemá `ILogger`:
  - Neloguje se: úspěšný login, neúspěšný login, lockout, změna hesla, reset hesla, 2FA enable/disable, admin akce, session invalidace, device registration/revocation
- **Kafka sink zahazuje structured properties** — `ConfluentKafkaSink` volá jen `logEvent.RenderMessage()`, properties (`UserId`, `ClientIp`) se ztratí
- **Chat a Notifications nemají žádné logování** — ani Serilog, ani request logging
- **4 z 8 služeb neposílají do Kafka** — Payments, Cards, Products chybí Kafka sink; Chat, Notifications nemají ani Serilog
- **Žádné `EnrichDiagnosticContext`** — request logy neobsahují UserId, ClientIp, RequestBody

---

## 5. Rate limiting — 18%

### Co existuje

- **Eskalující DB-persisted account lockout** — `User.RecordFailedLogin()`:
  - 5 pokusů → 10 min lock
  - 8 pokusů → 1 h lock
  - 12+ pokusů → 24 h lock
  - `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs:159-171`
- **HTTP 429 response** — endpoint vrací `LoginLockoutResponse` s přesným časem odblokování
  - `src/Services/Identity/FairBank.Identity.Api/Endpoints/UserEndpoints.cs:55-74`
- **Client-side fast path** — `AuthService.cs:71-73` drží `_lockedUntil` v paměti (dokumentováno jako non-security control)
- **LoginHandler kontroluje lockout před i po ověření hesla** — `LoginUserCommandHandler.cs:34-45`

### Co chybí

- **Žádný ASP.NET Core `AddRateLimiter`/`UseRateLimiter`** — globální search: 0 výsledků
- **Žádný IP-based rate limiting** — lockout je per-account. Credential stuffing (tisíce různých účtů) není blokován
- **Nginx nemá `limit_req_zone`** — žádné IP-level throttling
- **Neomezená registrace** — `POST /api/v1/users/register` bez limitu
- **Neomezený password reset** — `POST /api/v1/users/forgot-password` bez limitu (SMTP abuse)
- **Neomezená 2FA verifikace** — `POST /api/v1/users/2fa/verify` bez limitu. 6-digit TOTP = 1M kombinací, brute-force možný
- **Neomezený resend-verification** — `POST /api/v1/users/resend-verification` — spam inbox
- **Žádný gateway-level rate limiting** — YARP je čistý passthrough proxy

---

## 6. Anti-bot ochrana — 0%

### Co existuje

**Nic.** Kompletní search přes celý `src/` na: captcha, recaptcha, hcaptcha, turnstile, honeypot, bot detection — 0 výsledků.

### Co chybí

- **CAPTCHA na loginu** — bot může zkoušet tisíce účtů za sekundu
- **CAPTCHA na registraci** — automatizovaná tvorba účtů je zcela neomezená
- **CAPTCHA na password reset** — SMTP abuse
- **Honeypot pole** — žádná skrytá pole ve formulářích
- **User-Agent validace** — nginx forwarduje, ale nikdo nekontroluje
- **Behavioral analysis** — žádná detekce mouse movement, keystroke timing

---

## 7. CORS ochrana — 20%

### Co existuje

- **Chat + Notifications** — `AddCors("AllowAll")` s `SetIsOriginAllowed(_ => true)` a `AllowCredentials()`
  - `src/Services/Chat/FairBank.Chat.Api/Program.cs:23-30,47`
  - `src/Services/Notifications/FairBank.Notifications.Api/Program.cs:16-23,40`
- **Nginx preflight handling** — pro `/chat-hub` endpoint (OPTIONS → 204 s CORS hlavičkami)
- **Docker network isolation** — backend služby jsou na `internal: true` síti, nereachable zvenku přímo

### Co chybí

- **API Gateway nemá žádnou CORS konfiguraci** — `Program.cs` neobsahuje `AddCors`/`UseCors`
- **5 z 7 služeb nemá CORS** — Identity, Accounts, Cards, Payments, Products (mitigováno Docker izolací)
- **AllowAll je nejslabší konfigurace** — `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` = libovolný origin může dělat credentialed requesty na Chat/Notifications
- **Žádný origin allowlist** — nikde v projektu neexistuje seznam povolených originů (mělo by být `http://localhost`, production domain)
- **`/notification-hub` nemá nginx location block** — propadne do generic `/api/` proxy bez CORS headers

---

## 8. DB redundance — 35%

### Co existuje

- **PostgreSQL streaming replication** — `wal_level=replica`, `max_wal_senders=3`, `hot_standby=on`
  - `docker-compose.yml:207-218`
- **Replica s `pg_basebackup`** — korektní inicializace, `standby.signal`, WAL streaming
  - `docker/postgres/replica-entrypoint.sh`
- **Oddělené Docker volumes** — `pgdata-primary`, `pgdata-replica`
- **Replication user** — `replicator` s `REPLICATION LOGIN`, `pg_hba.conf` řádek
  - `docker/postgres/primary-init.sh`
- **Schema isolation** — 7 schémat v jedné DB, per-service `search_path`
  - `docker/postgres/init.sql`
- **Data checksums** — `POSTGRES_INITDB_ARGS: "--data-checksums"`

### Co chybí

- **Replica se nikdy nepoužívá** — žádná služba nemá `ReadOnlyConnection`, všechny connection stringy ukazují jen na `postgres-primary`
- **Žádný automatický failover** — žádný Patroni/repmgr/pg_auto_failover. Výpadek primary = totální nedostupnost všech služeb
- **Žádné zálohy** — žádný pg_dump, wal-g, pgbackrest, barman; `archive_mode` není nastavený
- **Žádný replication slot** — pokud replica zaostane >64MB WAL (wal_keep_size), ztratí synchronizaci a vyžaduje nový pg_basebackup
- **`pg_hba.conf` povoluje replikaci z `all` IP** — mělo by být omezeno na Docker subnet
- **Žádný monitoring replikace** — nesleduje se replication lag

---

## 9. Soft delete účtů — 45%

### Co existuje

- **`User.SoftDelete()` + `User.Restore()`** — správně implementované doménové metody
  - `src/Services/Identity/FairBank.Identity.Domain/Entities/User.cs:137-151`
  - Nastavuje: `IsDeleted=true`, `IsActive=false`, `DeletedAt=DateTime.UtcNow`
- **EF Core global query filter** — `builder.HasQueryFilter(u => !u.IsDeleted)` automaticky filtruje smazané
  - `src/Services/Identity/FairBank.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs:121`
- **`DeleteUserCommandHandler`** — volá `user.SoftDelete()`, žádný hard delete
  - `src/Services/Identity/FairBank.Identity.Application/Users/Commands/DeleteUser/DeleteUserCommandHandler.cs:12-22`
- **`PaymentTemplate.SoftDelete()`** — s EF query filterem
  - `src/Services/Payments/FairBank.Payments.Domain/Entities/PaymentTemplate.cs:88-92`
  - `src/Services/Payments/FairBank.Payments.Infrastructure/Persistence/Configurations/PaymentTemplateConfiguration.cs:44`
- **DELETE endpoint existuje** — `UserEndpoints.cs:316-322`

### Co chybí

- **Žádný Restore endpoint** — `User.Restore()` je dead code; žádný `RestoreUserCommand`, handler, ani endpoint. Implementace restore by vyžadovala `IgnoreQueryFilters()` protože global filter skrývá smazané
- **Delete endpoint nemá autorizaci** — `MapDelete("/{id:guid}")` bez `.RequireAuthorization()`, kdokoliv může smazat kohokoliv
- **Žádná cross-service kaskáda** — soft delete v Identity nenotifikuje Accounts, Cards, Payments, Chat. Účty, karty, platby, šablony smazaného uživatele zůstávají aktivní a přístupné
- **SavingsGoal delete je no-op** — handler validuje existenci, ale nic nesmaže (komentář: "future iteration")
  - `src/Services/Accounts/FairBank.Accounts.Application/Commands/DeleteSavingsGoal/DeleteSavingsGoalCommandHandler.cs:10-16`
- **Žádný soft delete na Cards, Accounts, Notifications, Products, Chat**
- **PaymentTemplate nemá `DeletedAt` timestamp** — jen `IsDeleted` + `UpdatedAt`

---

## 10. Scénáře výpadku (resilience návrh) — 25%

### Co existuje

- **PostgreSQL healthcheck** — primary i replica s `pg_isready`, interval 10s, 5 retries
  - `docker-compose.yml:219-223,243-247`
- **`EnableRetryOnFailure(3)`** — na 4 ze 7 služeb (Identity, Payments, Products, Cards)
  - `src/Services/Identity/FairBank.Identity.Infrastructure/DependencyInjection.cs:25`
  - `src/Services/Payments/FairBank.Payments.Infrastructure/DependencyInjection.cs:24`
  - `src/Services/Products/FairBank.Products.Infrastructure/DependencyInjection.cs:20`
  - `src/Services/Cards/FairBank.Cards.Infrastructure/DependencyInjection.cs:19`
- **Health endpointy** — každá služba má `GET /health`, YARP routuje health checks pro identity, accounts, payments
- **HTTP timeouty** — inter-service HttpClient s `TimeSpan.FromSeconds(5-10)`
- **`depends_on: condition: service_healthy`** — služby čekají na zdravý postgres před startem

### Co chybí

- **Žádné Docker healthchecky na mikroslužbách** — pouze postgres, žádná API služba nemá HEALTHCHECK v docker-compose (Dockerfile má `wget` check, ale compose ho nepoužívá)
- **Žádná restart policy** — pouze `products-api` má `restart: unless-stopped`; gateway, identity, accounts, payments, cards, chat, notifications — nic. Crash = permanentní výpadek
- **Žádný circuit breaker (Polly)** — inter-service HttpClient volání nemají retry pattern, circuit breaker, ani fallback. Globální search na Polly: 0 výsledků
- **Chat a Notifications nemají DB retry** — plain `UseNpgsql(connectionString)` bez `EnableRetryOnFailure`
- **Žádný automatický failover DB** — viz sekce 8
- **YARP nemá active health checks** — nepoluje `/health` endpointy; při selhání služby vrací 502 Bad Gateway přímo klientovi
- **Health endpointy jsou statické** — vrací `{ "Status": "Healthy" }` vždy, nekontrolují DB konektivitu ani závislosti
- **Inter-service `condition: service_started`** — nekontroluje readiness, jen start kontejneru

---

## 11. Ochrana před zranitelnostmi — matice hrozeb

| Hrozba | Ochrana v kódu | Stav | Skóre |
|--------|---------------|------|-------|
| **SQL Injection** | EF Core + Marten parameterized queries; žádný `FromSqlRaw`/`ExecuteSqlRaw` s user inputem. Jediný edge case: `EF.Functions.ILike` v chat search nepropouští `%`/`_` wildcardy (logic issue, ne injection). Cards startup má raw DDL ale bez user inputu. | Výborný | **95%** |
| **XSS** | Blazor WASM auto-escaping aktivní, žádný `MarkupString` v kódu. API vrací jen JSON. **ALE:** Chat attachment endpoint (`/api/v1/chat/messages/{id}/attachments`) nevaliduje content-type, neomezuje velikost, servíruje soubory zpět s client-supplied ContentType — stored XSS vektor. | Dobrý s dírou | **70%** |
| **BOLA** | **KRITICKÉ.** Téměř žádná ownership verifikace. Výčet zranitelných endpointů: Accounts (7 endpointů), Cards (6), Payments (4+), Identity (6+), Chat (4+), Notifications (2), Products (2). Celkem 30+ endpointů přijímá libovolné GUID bez kontroly vlastnictví. Jedinou výjimkou je `RevokeDeviceCommand` a `TrustDeviceCommand` které extrahují userId z Bearer tokenu. | Kriticky chybí | **5%** |
| **Token reuse** | Session token = `Base64(userId:sessionId)`, nepodepsaný (žádný HMAC). SessionId je krypto-random UUID (122 bitů entropie) — v praxi neuhodnutelný. Server-side: single-session enforcement, logout invaliduje session, server-enforced expiry (8h). Token v localStorage (XSS-vulnerable). | Server-side OK, transport slabý | **60%** |
| **Brute force login** | Eskalující lockout (5/8/12 pokusů), HTTP 429, DB-persisted. Přežije restart serveru. | Per-account OK, žádný IP limit | **40%** |
| **Credential stuffing** | Nic — žádný IP rate limit, žádný CAPTCHA, žádný bot detection | Kompletně chybí | **0%** |
| **CSRF** | JWT/session v Authorization headeru, ne cookie. Blazor WASM architektura. Chat upload má `.DisableAntiforgery()` ale je to OK pro non-cookie auth. | Správný design | **90%** |
| **Session hijacking** | 8h session, single-session enforcement, server-enforced expiry. ALE: token v localStorage (XSS risk), žádná token rotation, žádný per-request gateway validation. | Částečný | **55%** |
| **Double-click / Replay** | Žádný idempotency key (design doc zmiňoval `X-Idempotency-Key`, neimplementováno). Platby a transakce nemají ochranu proti duplicitním submits. | Kompletně chybí | **0%** |
| **File upload abuse** | Chat attachment: žádná content-type allowlist, žádný extension whitelist, žádný file size limit, originální extension zachována, client-supplied ContentType na download. Stored XSS vektor. | Zranitelný | **10%** |

---

## Prioritní doporučení pro implementaci

### Kritická (před nasazením do produkce)

1. **BOLA fix** — Implementovat per-request identity propagaci. YARP gateway by měl dekódovat Bearer token a forwardovat `X-User-Id` + `X-User-Role` hlavičky. Každý handler musí porovnat authenticated userId s OwnerId zdroje. Systémová změna napříč všemi 7 službami.

2. **RBAC enforcement** — Přidat `.RequireAuthorization("PolicyName")` na všechny endpointy. Implementovat `ClientOnly`, `BankerOnly`, `AdminOnly` policies. Gateway-level JWT/session validace per-request.

3. **Chat attachment** — Content-type allowlist (`image/jpeg`, `image/png`, `application/pdf`), validace magic bytes, max file size (10 MB), `Content-Disposition: attachment` na download.

### Vysoká (před spuštěním)

4. **Token signing** — Nahradit Base64 encoding za HMAC-SHA256 podpis nebo plný JWT s HS256. Eliminuje teoretický forgery vektor.

5. **HTTPS/TLS** — Self-signed cert na nginx pro dev, Let's Encrypt pro produkci. Přidat `SslMode=Require` do DB connection stringů.

6. **Rate limiting** — ASP.NET Core `AddRateLimiter` na gateway: 100 req/min globálně, 5 req/min na auth endpointy. Nginx `limit_req_zone` jako backup.

7. **Audit logging** — `IAuditService` s explicit log events: login success/fail, lockout, password change, admin actions. Structured properties (UserId, IP, Action, Outcome).

8. **Restart policies** — `restart: unless-stopped` na všechny Docker služby.

### Střední

9. **Anti-bot** — reCAPTCHA/hCaptcha na registraci a password reset.

10. **CORS** — Origin allowlist na Chat/Notifications (`http://localhost`, production domain). Přidat CORS na API Gateway.

11. **DB failover** — Patroni nebo repmgr pro automatický failover. Backup cron s pg_dump nebo wal-g.

12. **Soft delete cascade** — Domain event z DeleteUser → deaktivace účtů, karet, platebních šablon. Restore endpoint s admin-only auth.

13. **Resilience** — Polly circuit breaker na inter-service HTTP klienty. Docker healthchecky na všechny služby. YARP active health checks.
