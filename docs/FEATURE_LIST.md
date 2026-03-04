# FairBank — Feature List

**Version**: `b5402c1` (main, 2026-03-04)

---

## Technology Stack

### Language & Runtime

| Technology | Version |
|---|---|
| C# / .NET | **10.0** (`net10.0` target framework) |
| .NET SDK (build) | `mcr.microsoft.com/dotnet/sdk:10.0-alpine` |
| .NET ASP.NET Runtime | `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` |
| Nullable reference types | Enabled globally |
| Implicit usings | Enabled globally |
| Warnings as errors | Enabled globally |

### Frontend

| Technology | Version | Purpose |
|---|---|---|
| Blazor WebAssembly | 10.0.3 | SPA framework (client-side WASM) |
| ASP.NET SignalR Client | 10.0.3 | Real-time communication (Chat) |
| Razor Class Libraries | — | Modular UI (Auth, Overview, Payments, Cards, Savings, Investments, Exchange, Products, Profile) |
| Vanilla CSS | — | Custom "Vabank" design system |
| nginx (Alpine) | latest | Static file server + reverse proxy for WASM |

### Backend — Microservices

| Technology | Version | Purpose |
|---|---|---|
| ASP.NET Core Minimal APIs | 10.0 | HTTP endpoints (8 services) |
| MediatR | 14.0.0 | CQRS command/query dispatch |
| FluentValidation | 12.1.1 | Request validation |
| Entity Framework Core | 10.0.3 | ORM / data access |
| Npgsql (EF Core provider) | 10.0.0 | PostgreSQL driver |
| Marten | 8.22.1 | Event sourcing (Payments service) |
| BCrypt.Net-Next | 4.0.3 | Password hashing |
| QRCoder | 1.6.0 | QR code generation (SPAYD) |
| ClosedXML | 0.105.0 | XLSX document generation |
| DocumentFormat.OpenXml | 3.1.1 | DOCX document generation |

### API Gateway

| Technology | Version | Purpose |
|---|---|---|
| YARP (Yet Another Reverse Proxy) | 2.3.0 | Request routing to microservices |
| ASP.NET Rate Limiting | built-in | Fixed-window rate limiting (3 tiers) |
| In-Memory Cache | built-in | Session validation cache (30s TTL) |

### Logging & Observability

| Technology | Version | Purpose |
|---|---|---|
| Serilog.AspNetCore | 10.0.0 | Structured logging framework |
| Serilog.Sinks.Console | 6.1.1 | Console log output |
| Confluent.Kafka | 2.3.0 | Kafka producer (custom Serilog sink) |
| Apache Kafka | latest (KRaft) | Centralized log streaming / message broker |

### API Documentation

| Technology | Version | Purpose |
|---|---|---|
| Microsoft.AspNetCore.OpenApi | 10.0.3 | OpenAPI spec generation |
| Scalar.AspNetCore | 2.4.12 | Interactive API documentation UI |

### Database & Infrastructure

| Technology | Version | Purpose |
|---|---|---|
| PostgreSQL | 16 (Alpine) | Primary database (with WAL streaming replication) |
| PostgreSQL Replica | 16 (Alpine) | Read replica (hot standby) |
| Docker Compose | — | Container orchestration (13 services) |

### Testing

| Technology | Version | Purpose |
|---|---|---|
| xUnit | 2.9.3 | Test framework |
| xunit.runner.visualstudio | 3.1.5 | VS test adapter |
| Microsoft.NET.Test.Sdk | 18.3.0 | Test host |
| FluentAssertions | 8.8.0 | Assertion library |
| NSubstitute | 5.3.0 | Mocking framework |
| Testcontainers.PostgreSql | 4.10.0 | Integration test containers |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.3 | Integration test server |
| EF Core SQLite (in-memory) | 10.0.3 | Lightweight test database |

### Authentication & Security

| Technology | Version | Purpose |
|---|---|---|
| ASP.NET Authentication.Cookies | 10.0.3 | Cookie-based auth support |
| Custom session token system | — | Base64-encoded UserId+SessionId tokens |
| TOTP (2FA) | — | Time-based one-time passwords with backup codes |
| Internal API Key (`X-Internal-Api-Key`) | — | Inter-service authentication |

### Architecture Summary

| Layer | Technology |
|---|---|
| Frontend | Blazor WASM (modular Razor Class Libraries) served by nginx |
| API Gateway | YARP Reverse Proxy (.NET 10) |
| Backend | 8 independent microservices (.NET 10 Minimal APIs) |
| Messaging | MediatR 14 (CQRS), Kafka (centralized log streaming) |
| Database | PostgreSQL 16 (primary + streaming replica) |
| Logging | Serilog 10 → Console + Kafka sink |
| Deployment | Docker Compose (13 containers on Alpine Linux) |

---

## 1. Identity & Authentication

- **User Registration** — email, password, first/last name; auto-provisions Checking + Savings CZK accounts on signup.
- **Login** — email/password with brute-force lockout (429 Too Many Requests after repeated failures).
- **Logout** — server-side session invalidation.
- **Session Management** — token-based sessions with server-side validation; inactivity timeout; `AuthGuard` frontend component.
- **Email Verification** — verify email address with code; resend verification endpoint.
- **Password Reset** — forgot password + reset password flow.
- **Two-Factor Authentication (2FA)** — TOTP setup, enable with backup codes, verify, and disable.
- **Device Management** — register devices, revoke devices, trust devices.
- **Security Settings** — per-user configurable security preferences (get/set).
- **Profile Editing** — change email, change password (BOLA-protected – users can only edit their own data).
- **Role-Based Access** — Admin, Banker, Client, Child roles enforced across endpoints.

## 2. Accounts

- **Create Account** — checking or savings; CZK currency.
- **List Accounts** — by owner; includes balances.
- **Get Account by ID** — detailed view.
- **Close Account** — soft-close.
- **Transactions** — list transactions per account; create pending transactions; approve transactions.

## 3. Savings

- **Savings Goals** — create, list by account, deposit to, withdraw from, delete.
- **Savings Rules** — create automated savings rules, list by account, toggle on/off.

## 4. Investments

- **Create Investment** — tied to an account.
- **List Investments** — by account.
- **Get Investment by ID**.
- **Update Investment Value** — mark-to-market updates.
- **Sell Investment** — liquidate position.

## 5. Payments

- **Send Payment** — domestic transfers between accounts.
- **List Payments** — by account with configurable limit.
- **Search Payments** — full-text search within account payments.
- **Payment Statistics** — aggregated statistics by period (monthly, etc.) with date range filtering.
- **Payment Export** — export transactions to CSV (or other formats) with date filtering.
- **Set Payment Category** — categorize individual payments.
- **QR Code Generation** — generate SPAYD QR codes for payment requests.
- **QR Code Parsing** — parse SPAYD QR data into structured payment fields.
- **Standing Orders** — create recurring payments, list by account, cancel.
- **Payment Templates** — create reusable payment templates, list by account, delete; admin bulk-deactivate.

## 6. Currency Exchange (Směnárna)

- **Get Exchange Rate** — real-time rates between currency pairs.
- **Execute Exchange** — convert between currencies with validation.
- **Exchange History** — per-user transaction history with configurable limit.
- **Favorite Pairs** — add, list, and remove favorite currency pairs.

## 7. Cards

- **Issue Card** — create a new card for an account.
- **List Cards** — by account or by user.
- **Get Card by ID**.
- **Block / Unblock Card** — instant card blocking and unblocking.
- **Cancel Card** — permanent cancellation.
- **Set Card Limits** — configure spending limits.
- **Set Card Settings** — toggle contactless, online payments, etc.
- **Set PIN** — change card PIN.
- **Renew Card** — issue a replacement card.
- **Admin: Deactivate All Cards** — bulk-deactivate all cards for a given user.

## 8. Products & Applications

- **Submit Application** — apply for banking products (loans, mortgages, insurance).
- **Get User Applications** — BOLA-protected listing.
- **Get Pending Applications** — Banker/Admin queue.
- **Get Application by ID**.
- **Approve / Reject / Cancel Application** — full lifecycle workflow (Banker/Admin only for approve/reject).

### Frontend Product Tools
- **Loan Calculator** — interactive slider-based loan calculator.
- **Mortgage Calculator** — with fixation period options.
- **Insurance Panel** — insurance product exploration.
- **My Products Panel** — view active products.

## 9. Documents

- **Generate Account Statement** — export statements in PDF, DOCX, or XLSX format with date range filtering.

## 10. Chat / Messaging

- **Chat Service** — real-time messaging between users and bank staff.
- **Conversation List** — view all conversations.
- **Chat Detail** — individual conversation with message bubbles.
- **File Validation** — server-side validation of uploaded files.

## 11. Notifications

- **Create Notification** — system-generated notifications.
- **List Notifications** — paginated, filterable by type (BOLA-protected).
- **Unread Count** — real-time badge count (polled every 30s in the UI).
- **Mark as Read** — individual or bulk mark-all-as-read.
- **Delete Notification**.
- **Notification Preferences** — per-user preferences (get/update).

## 12. Admin Panel

- **Admin Dashboard** (`/admin`) — centralized admin controls.
- **User Management** — list all users, activate/deactivate, change roles, restore soft-deleted users.
- **Audit Logs** (`/admin/logs`) — comprehensive audit trail with:
  - Paginated table with sortable columns (Timestamp, UserId, Action, EntityName).
  - Full filtering: partial text search on Action, EntityName, Details; date/time range with `datetime-local`; GUID-based user ID search.
  - Records login attempts, registration, role changes, account activation/deactivation, email changes, 2FA events, password resets, device management, and more.

## 13. Family / Parental Controls

- **Create Child Account** — parent can create child sub-accounts.
- **Child Detail** — view child account details.
- **Family Overview** — manage family members.
- Role-restricted: Child accounts have limited feature access (no savings goals, investments, or currency exchange).

## 14. UI / UX

- **Responsive Design** — mobile-first with desktop sidebar navigation.
- **Dark Mode** — full dark/light theme toggle with system persistence.
- **User Name Display** — visible in sidebar (desktop) and top bar (mobile).
- **Bottom Navigation** — mobile tab bar with arc/circular quick-action menu.
- **Animated Transitions** — staggered `fadeInUp` animations on page content.
- **Premium Vabank Theme** — custom CSS design system with typographic hierarchy, card components, badges, and color palettes.

## 15. Security & Infrastructure

- **API Gateway** — YARP reverse proxy routing all traffic to backend microservices.
- **Auth Middleware** — gateway-level session validation; sets `X-User-Id` and `X-User-Role` headers for downstream services.
- **Rate Limiting** — three tiers:
  - `auth` (5 req/min) for login/register/2FA.
  - `sensitive` (10 req/min) for password reset, email verification.
  - `global` (200 req/min) for general API calls.
- **Internal API Key** — all inter-service communication authenticated via `X-Internal-Api-Key` header; gateway strips client-supplied keys to prevent spoofing.
- **BOLA Protection** — Broken Object Level Authorization checks on user-specific endpoints.
- **Session Caching** — in-memory cache (30s TTL) at the gateway to avoid per-request Identity service calls.
- **PostgreSQL Replication** — primary/replica streaming replication with WAL-based sync.
- **Kafka** — centralized log streaming from all services.
- **Health Checks** — every container exposes `/health` with Docker healthcheck probes.
- **CORS** — configured for local development and Docker networking.

## 16. Testing

- Unit tests across multiple services:
  - Identity (login audit, 2FA, security settings, device management, restore user)
  - Accounts (savings goals, transactions)
  - Payments (template deactivation)
  - Cards (card deactivation)
  - Chat (file validation)
  - Documents (statement generation)
  - API Gateway (auth middleware, rate limiting)
  - SharedKernel (audit logger, Kafka sink, authorization filters)
