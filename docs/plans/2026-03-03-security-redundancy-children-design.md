# Security, DB Redundancy & Child Accounts — Design Document

> **Schváleno:** 2026-03-03
> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement the corresponding implementation plan.

**Goal:** Add complete security layer (JWT auth, RBAC, rate limiting, CORS, audit logging, encryption), PostgreSQL primary-replica redundancy, child accounts with parental oversight, and wire Blazor WASM frontend to real API endpoints.

**Architecture:** Extend existing Identity and Accounts microservices. JWT issued by Identity, validated by API Gateway. PostgreSQL streaming replication for read scaling and failover demonstration. Parent-child relationship in Identity domain, spending limits and pending transaction approval in Accounts domain.

**Tech Stack:** BCrypt.Net-Next (password hashing), Microsoft.AspNetCore.Authentication.JwtBearer, System.IdentityModel.Tokens.Jwt, PostgreSQL streaming replication, ASP.NET Core Rate Limiting, Blazor AuthenticationStateProvider.

---

## 1. PostgreSQL Primary + Replica

### Docker Compose Changes

Replace single `postgres` service with two services:

**`postgres-primary`:**
- Image: `postgres:16-alpine`
- Container: `fairbank-pg-primary`
- Port: `5432:5432`
- WAL config: `wal_level=replica`, `max_wal_senders=3`, `wal_keep_size=64MB`, `hot_standby=on`
- Init scripts: `00-primary.sh` (creates replication role, configures `pg_hba.conf`), `01-init.sql` (existing schemas + users)

**`postgres-replica`:**
- Image: `postgres:16-alpine`
- Container: `fairbank-pg-replica`
- Port: `5433:5432`
- Custom entrypoint: `replica-entrypoint.sh` (runs `pg_basebackup` from primary, creates `standby.signal`, starts PostgreSQL)
- Depends on: `postgres-primary` (healthy)

### Connection Strings

- Identity API: `DefaultConnection` → primary, `ReadOnlyConnection` → replica
- Accounts API: `DefaultConnection` → primary only (event sourcing requires consistent writes)

### Replication User

- Username: `replicator`
- Password: `replicator_2026`
- Privileges: `REPLICATION LOGIN`

---

## 2. JWT Authentication + RBAC

### Token Architecture

- **Access Token (JWT):** 15 minute lifetime, signed with symmetric key (HS256)
- **Refresh Token:** 7 day lifetime, stored in `identity_service.refresh_tokens` table, single-use
- **One active session per user:** New login revokes previous refresh token
- **Issuer:** `fairbank-identity`, **Audience:** `fairbank-api`

### JWT Claims

```json
{
  "sub": "user-guid",
  "email": "user@example.com",
  "role": "Client",
  "firstName": "Jan",
  "parentId": "guid-or-null",
  "iat": 1709500000,
  "exp": 1709500900
}
```

### New Identity Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | `/api/v1/auth/login` | Email + password → JWT + refresh token | Public |
| POST | `/api/v1/auth/refresh` | Refresh token → new JWT + new refresh token | Public |
| POST | `/api/v1/auth/logout` | Revoke refresh token | Authenticated |
| GET | `/api/v1/users/me` | Current user profile from JWT claims | Authenticated |

### Password Hashing

Upgrade from SHA256 to BCrypt (`BCrypt.Net-Next` package, work factor 12).

### RefreshToken Entity

```csharp
public sealed class RefreshToken : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; }       // cryptographically random
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
```

Table: `identity_service.refresh_tokens`

### RBAC Policies

Configured in API Gateway:

| Policy | Allowed Roles |
|--------|--------------|
| `ClientOnly` | Client, Admin |
| `BankerOnly` | Banker, Admin |
| `AdminOnly` | Admin |
| `ParentOfChild` | Client (custom handler verifies parent-child relationship) |

### Gateway JWT Middleware

API Gateway validates JWT on every request (except `/api/v1/auth/*` and health endpoints). Services receive validated claims via forwarded headers or direct JWT parsing.

---

## 3. Child Accounts with Parental Oversight

### Design Principle

A parent is an ordinary `Client` user. No special role. A Client becomes a "parent" when they create a child account. The `ParentId` field on the child's `User` entity links them.

### Identity Domain Changes

**User entity — new fields:**
- `Guid? ParentId` — nullable self-reference FK to `users.Id`
- `ICollection<User> Children` — navigation property (parent → children)
- `User? Parent` — navigation property (child → parent)

**Validation rules:**
- `ParentId` is required when `Role == Child`
- `ParentId` must reference a user with `Role == Client`
- A child cannot have children (no nested parenting)
- `ParentId` is null for Client, Banker, Admin

### Accounts Domain Changes

**Account aggregate — new fields:**
- `Money? SpendingLimit` — daily spending limit (nullable, set by parent)
- `bool RequiresApproval` — whether transactions need parent approval
- `Money? ApprovalThreshold` — amount above which approval is required

**New aggregate: `PendingTransaction`** (event-sourced via Marten)
- `Guid Id`
- `Guid AccountId`
- `Money Amount`
- `string Description`
- `Guid RequestedBy` (child user ID)
- `Guid? ApproverId` (parent user ID, set on resolution)
- `PendingTransactionStatus Status` (Pending, Approved, Rejected)
- `string? RejectionReason`
- `DateTime CreatedAt`
- `DateTime? ResolvedAt`

### New Domain Events

```
SpendingLimitSet(AccountId, Limit, Currency, OccurredAt)
ApprovalRequirementSet(AccountId, RequiresApproval, Threshold?, Currency?, OccurredAt)
TransactionRequested(TransactionId, AccountId, Amount, Currency, Description, RequestedBy, OccurredAt)
TransactionApproved(TransactionId, ApproverId, OccurredAt)
TransactionRejected(TransactionId, ApproverId, Reason, OccurredAt)
```

### New API Endpoints

**Identity Service:**

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | `/api/v1/users/{parentId}/children` | Parent creates child account | Client (own children only) |
| GET | `/api/v1/users/{parentId}/children` | List parent's children | Client (own children only) |

**Accounts Service:**

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| GET | `/api/v1/accounts?ownerId={guid}` | List accounts by owner | Authenticated (own or parent's children) |
| POST | `/api/v1/accounts/{id}/limits` | Set spending limit | Client (parent of account owner) |
| GET | `/api/v1/accounts/{id}/pending` | List pending transactions | Client (parent) |
| POST | `/api/v1/accounts/pending/{id}/approve` | Approve transaction | Client (parent) |
| POST | `/api/v1/accounts/pending/{id}/reject` | Reject transaction | Client (parent) |

### Withdrawal Flow for Child Accounts

1. Child initiates withdrawal
2. Handler checks `Account.RequiresApproval` and `Account.ApprovalThreshold`
3. If amount <= threshold: execute immediately, notify parent
4. If amount > threshold: create `PendingTransaction`, notify parent
5. Parent approves/rejects via dedicated endpoints
6. On approval: handler executes the actual withdrawal from the account

---

## 4. Security Layer

### 4.1 Encryption

**In-transit:** TLS termination at nginx (self-signed cert for dev). Internal Docker network unencrypted (standard for single-host Compose).

**At-rest:** Documented as production recommendation. Dev environment without disk encryption.

### 4.2 Rate Limiting

In API Gateway using ASP.NET Core built-in rate limiter:
- Global: 100 requests/min per IP (fixed window)
- Auth endpoints: 5 requests/min per IP (anti-brute-force)

### 4.3 CORS

Configured in API Gateway. Origins: `http://localhost`, production domain. Allow credentials.

In practice, nginx serves both frontend and proxies API on the same origin (port 80), so CORS is a safety net.

### 4.4 Audit Log

New entity `AuditLog` in Identity Infrastructure:
- `Action` (string): "UserLoggedIn", "LoginFailed", "TransactionApproved", etc.
- `UserId` (Guid?): acting user
- `IpAddress` (string)
- `Details` (string): JSON payload
- `Timestamp` (DateTime)

Table: `identity_service.audit_logs`

Middleware in API Gateway captures: logins, failed attempts, account changes, sensitive access, anomalies.

### 4.5 Protection Matrix

| Threat | Protection |
|--------|-----------|
| SQL Injection | EF Core + Marten parameterized queries (existing) |
| XSS | Blazor WASM auto-escapes output; API returns JSON only |
| BOLA | Every handler verifies `userId == resource.OwnerId` or parent relationship |
| Token reuse | Refresh tokens are single-use, revoked after use |
| Brute force | Rate limiting on `/auth/login` (5/min) |
| Double-click | Idempotency key header (`X-Idempotency-Key`) on POST requests |
| CSRF | Not applicable — JWT in Authorization header, not cookies |
| Session hijacking | Short-lived JWT (15min), single active session per user |

### 4.6 Soft Delete

Already implemented for Users. Extensions:
- Only Admin can restore soft-deleted accounts (`POST /api/v1/users/{id}/restore`)
- Soft delete cascades: deactivates user's accounts

---

## 5. Frontend API Integration

### Auth in Blazor WASM

New `FairBank.Web.Auth` Razor Class Library:
- `Pages/Login.razor` — login form
- `Pages/Register.razor` — registration form
- `Services/JwtAuthStateProvider.cs` — custom `AuthenticationStateProvider`, reads JWT from localStorage
- `Services/AuthService.cs` — login/logout/refresh logic
- `Handlers/JwtAuthorizationHandler.cs` — `DelegatingHandler` adds Bearer header to every request

### App.razor Changes

Wrap Router in `<CascadingAuthenticationState>` with `<AuthorizeRouteView>`. Unauthenticated users redirect to login.

### IFairBankApi Extensions

```csharp
// Auth
Task<LoginResponse> LoginAsync(string email, string password);
Task RefreshTokenAsync(string refreshToken);
Task LogoutAsync();

// Users
Task<UserResponse?> GetCurrentUserAsync();
Task<List<UserResponse>> GetChildrenAsync();
Task<UserResponse> CreateChildAsync(string firstName, string lastName, string email, string password);

// Accounts
Task<List<AccountResponse>> GetMyAccountsAsync();
Task<AccountResponse> SetSpendingLimitAsync(Guid accountId, decimal limit);
Task<List<PendingTransactionDto>> GetPendingTransactionsAsync(Guid accountId);
Task ApproveTransactionAsync(Guid transactionId);
Task RejectTransactionAsync(Guid transactionId, string reason);
```

### Page Updates

- **Overview**: Call `GetMyAccountsAsync()` for real balance, display actual transactions
- **Payments**: Call real withdraw/deposit API
- **Profile**: Call `GetCurrentUserAsync()`, show children section if parent
- **Savings/Investments**: Remain demo data (no backend yet)

---

## 6. Bug Fixes Required

1. **MartenAccountEventStore.AppendEventsAsync**: Currently always calls `StartStream`. Must use `AppendToStream` for existing accounts (deposit/withdraw). `StartStream` only for new accounts.
