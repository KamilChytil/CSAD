# Feature: Loans, Insurance & Frontend Unification

> Branch: `feature/loans-and-insurance` (merged to main, branch deleted)
> Date: 2026-03-03 — 2026-03-04
> Commits: ~25 commits

---

## What Was Built

### 1. Products Microservice (Backend)

Full CQRS microservice for loan/insurance product applications.

**Domain Layer** (`src/Services/Products/FairBank.Products.Domain/`)
- `ProductApplication` aggregate with status lifecycle: Pending → Active/Rejected/Cancelled
- Enums: `ProductType` (PersonalLoan, Mortgage, TravelInsurance, PropertyInsurance, LifeInsurance, PaymentProtection), `ApplicationStatus`
- Repository interface `IProductApplicationRepository`

**Application Layer** (`src/Services/Products/FairBank.Products.Application/`)
- Commands: `SubmitApplication`, `ApproveApplication`, `RejectApplication`, `CancelApplication` (MediatR handlers)
- Queries: `GetUserApplications`, `GetPendingApplications`, `GetApplicationById`
- Validator: `SubmitApplicationCommandValidator` (FluentValidation)
- DTO: `ProductApplicationResponse`

**Infrastructure Layer** (`src/Services/Products/FairBank.Products.Infrastructure/`)
- EF Core `ProductsDbContext` with schema `products_service`
- `ProductApplicationRepository` implementing the domain port
- Entity configuration with value conversions for enums

**API Layer** (`src/Services/Products/FairBank.Products.Api/`)
- Minimal API endpoints: POST submit, GET user apps, GET pending, GET by ID, PUT approve, PUT reject, PUT cancel
- Dockerfile, appsettings, DI registration

**Docker Integration:**
- `products-api` service in docker-compose.yml
- YARP gateway route `/api/v1/products/{**catch-all}`
- PostgreSQL schema `products_service` with grants in init.sql

### 2. Products Frontend (Blazor WASM)

**New project:** `src/FairBank.Web.Products/`

- `Products.razor` — Main page with 4 tabs (Osobní úvěr, Hypotéka, Pojištění, Moje produkty)
- `LoanCalculatorPanel.razor` — Personal loan calculator with amount/months sliders, interest rate, RPSN, submit modal
- `MortgageCalculatorPanel.razor` — Mortgage calculator with property price, LTV, fixation period
- `InsurancePanel.razor` — 4 insurance sub-tabs:
  - Cestovní (travel) — destination, days, persons, variant
  - Nemovitost (property) — type, value, contents toggle
  - Životní (life) — age, coverage, risk/investment variant
  - Ochrana splátek (payment protection) — loan payment, variant
- `MyProductsPanel.razor` — Client's submitted applications with status badges, cancel option
- `Management.razor` — Banker's view at `/sprava` to approve/reject pending applications with notes
- Calculator services: `LoanCalculator`, `MortgageCalculator`, `InsuranceCalculator`
- API client methods in `FairBankApiClient` + `IFairBankApi` interface

### 3. Unit Tests

**68 tests** in `tests/FairBank.Products.UnitTests/`:
- Domain: `ProductApplicationTests` — creation, status transitions, validation, edge cases (28 tests)
- Commands: Approve/Reject/Cancel/Submit handler tests (30 tests)
- Queries: GetUserApplications, GetPendingApplications, GetApplicationById (8 tests)
- Validators: SubmitApplicationCommandValidator (2 tests)

**Calculator tests** in `tests/FairBank.Web.Products.Tests/`:
- `LoanCalculatorTests`, `MortgageCalculatorTests`, `InsuranceCalculatorTests`

Stack: xUnit + FluentAssertions + NSubstitute

### 4. Frontend Unification (Emoji → VbIcon SVG)

Replaced ALL emoji icons across the app with `<VbIcon>` SVG components for visual consistency.

**New icons added to VbIcon.razor:** exchange, heart, lock, eye, eye-off, warning, check, x-circle, ban, building, bank, clipboard

**Files modified:**
- `SideNav.razor`, `BottomNav.razor` — 💱 → `<VbIcon Name="exchange">`
- `Products.razor` — 💰🏠🛡️📋 → wallet/home/shield/clipboard
- `InsurancePanel.razor` — ✈️🏠❤️🔒🇪🇺🌍🏢🏡 → travel/home/heart/lock/building
- `MyProductsPanel.razor` — Refactored `GetProductIcon` → `GetProductIconName` returning VbIcon names, added `GetStatusIconName` for status badges
- `Management.razor` — Same refactor + ✅❌ buttons → check/x-circle VbIcon
- `ChatList.razor` — 💬🏦👨‍👩‍👧 → chat/bank/user VbIcon
- `Login.razor` — 🔒⚠️⏰🙈👁️ → lock/danger/clock/eye-off/eye
- `Register.razor` — ✅⚠️🙈👁️ → check/danger/eye-off/eye

### 5. Dead Code Cleanup

- Removed `AuthStateService.cs` (replaced by `IAuthService`/`AuthService`)
- Removed `AuthStateService` registration from `Program.cs`
- Removed legacy `/login-legacy` page (`src/FairBank.Web/Pages/Login.razor`)

### 6. Merge from Main

Resolved 7 merge conflicts when merging main's updates (banker tools, chat history, VbIcon rewrite):
- `VbIcon.razor` — Took main's inline SVG architecture, added our 8 missing icons
- `Login.razor` / `Register.razor` — Used main's "danger" icon name
- `ChatList.razor` — Took main's tab/RenderConv structure, applied VbIcon
- `ThemeService.cs` — Took main's cleaner version
- `docker-compose.yml` / `init.sql` — Merged both services

---

## API Endpoints Added

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/products/applications` | Submit new product application |
| GET | `/api/v1/products/applications/user/{userId}` | Get user's applications |
| GET | `/api/v1/products/applications/pending` | Get all pending (banker) |
| GET | `/api/v1/products/applications/{id}` | Get application by ID |
| PUT | `/api/v1/products/applications/{id}/approve` | Approve application |
| PUT | `/api/v1/products/applications/{id}/reject` | Reject application |
| PUT | `/api/v1/products/applications/{id}/cancel` | Cancel application |

---

## Design & Plan Documents

- `docs/plans/2026-03-03-loans-insurance-design.md` — Original feature design
- `docs/plans/2026-03-03-loans-insurance-implementation.md` — Implementation plan
- `docs/plans/2026-03-03-product-persistence-design.md` — Backend persistence design
- `docs/plans/2026-03-03-product-persistence-plan.md` — Backend implementation plan
- `docs/plans/2026-03-03-frontend-unification-design.md` — Emoji→VbIcon design
- `docs/plans/2026-03-03-frontend-unification-plan.md` — Emoji→VbIcon plan
