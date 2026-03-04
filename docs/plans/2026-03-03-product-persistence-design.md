# Product Persistence Design

**Goal:** Add backend persistence for product applications (loans, mortgages, insurance) with approval workflow and role-based access control.

**Architecture:** New `FairBank.Products` microservice following the same Clean Architecture + CQRS pattern as Payments service. EF Core with PostgreSQL, MediatR, Minimal APIs. Frontend gets new "Moje produkty" tab and admin approval page.

---

## Domain Model

### ProductApplication (Aggregate)

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid | Applicant (from Identity service) |
| ProductType | enum | PersonalLoan, Mortgage, TravelInsurance, PropertyInsurance, LifeInsurance, PaymentProtection |
| Status | enum | Pending, Approved, Rejected, Active, Cancelled |
| Parameters | string (JSON) | Product-specific parameters (amount, rate, term, etc.) |
| MonthlyPayment | decimal | Calculated monthly payment/premium |
| CreatedAt | DateTime | When application was submitted |
| ReviewedAt | DateTime? | When admin reviewed |
| ReviewedBy | Guid? | Admin who reviewed |
| Note | string? | Admin note (reason for rejection, etc.) |

### Status Workflow

```
Pending → Approved → Active → Completed
       → Rejected
Pending → Cancelled (by applicant)
```

- **Pending**: Submitted by client, waiting for admin/banker review
- **Approved**: Admin approved, product becomes active
- **Rejected**: Admin rejected with note
- **Active**: Product is in effect (loan disbursed, insurance active)
- **Cancelled**: Client cancelled pending application
- **Completed**: Loan fully repaid, insurance term ended (future)

### ProductType Parameters (stored as JSON)

**PersonalLoan:**
```json
{ "amount": 200000, "months": 60, "interestRate": 5.9, "rpsn": 6.1, "totalCost": 231420 }
```

**Mortgage:**
```json
{ "propertyPrice": 5000000, "loanAmount": 4000000, "years": 25, "fixation": 5, "interestRate": 4.79, "ltv": 80 }
```

**Insurance (Travel):**
```json
{ "destination": "europe", "variant": "standard", "days": 7, "persons": 2, "premium": 490 }
```

**Insurance (Property):**
```json
{ "propertyType": "apartment", "propertyValue": 3000000, "includeContents": false, "annualPremium": 2400 }
```

**Insurance (Life):**
```json
{ "age": 30, "coverage": 1000000, "variant": "risk" }
```

**Insurance (PaymentProtection):**
```json
{ "monthlyPayment": 5000, "variant": "standard" }
```

---

## Role-Based Access

| Role | View Calculators | Submit Applications | Review Applications |
|------|-----------------|--------------------|--------------------|
| Client | Yes | Yes | No |
| Child | **No** (section hidden) | **No** | No |
| Banker | Yes (informational) | No | Yes (approve/reject) |
| Admin | Yes (informational) | No | Yes (approve/reject) |

---

## API Endpoints

Base path: `/api/v1/products`

| Method | Path | Description | Allowed Roles |
|--------|------|-------------|---------------|
| POST | `/applications` | Submit new application | Client |
| GET | `/applications/user/{userId}` | Get user's applications | Client (own) |
| GET | `/applications/pending` | Get pending applications | Admin, Banker |
| GET | `/applications/all` | Get all applications | Admin, Banker |
| GET | `/applications/{id}` | Get application detail | Owner, Admin, Banker |
| PUT | `/applications/{id}/approve` | Approve application | Admin, Banker |
| PUT | `/applications/{id}/reject` | Reject application (with note) | Admin, Banker |
| PUT | `/applications/{id}/cancel` | Cancel pending application | Owner (Client) |

---

## Backend Service Structure

```
Services/Products/
├── FairBank.Products.Domain/
│   ├── Entities/ProductApplication.cs
│   ├── Enums/ProductType.cs, ApplicationStatus.cs
│   └── Repositories/IProductApplicationRepository.cs
├── FairBank.Products.Application/
│   ├── Commands/
│   │   ├── SubmitApplication/SubmitApplicationCommand.cs + Handler
│   │   ├── ApproveApplication/ApproveApplicationCommand.cs + Handler
│   │   ├── RejectApplication/RejectApplicationCommand.cs + Handler
│   │   └── CancelApplication/CancelApplicationCommand.cs + Handler
│   ├── Queries/
│   │   ├── GetUserApplications/GetUserApplicationsQuery.cs + Handler
│   │   ├── GetPendingApplications/GetPendingApplicationsQuery.cs + Handler
│   │   └── GetApplicationById/GetApplicationByIdQuery.cs + Handler
│   └── Dtos/ProductApplicationResponse.cs
├── FairBank.Products.Infrastructure/
│   ├── Persistence/
│   │   ├── ProductsDbContext.cs
│   │   ├── Configurations/ProductApplicationConfiguration.cs
│   │   └── Repositories/ProductApplicationRepository.cs
│   └── DependencyInjection.cs
└── FairBank.Products.Api/
    ├── Program.cs
    ├── Endpoints/ProductApplicationEndpoints.cs
    └── Dockerfile
```

---

## Infrastructure Changes

### PostgreSQL
- New schema: `products_service` in `init.sql`
- Table: `product_applications`

### Docker Compose
- New service: `products-api` container
- Depends on: `postgres-primary`
- Internal network only (accessed via API Gateway)

### YARP API Gateway
- New route: `/api/v1/products/{**catch-all}` → `products-api:8080`

### Solution
- Add 4 new projects to `FairBank.slnx`

---

## Frontend Changes

### 1. Products page — new "Moje produkty" tab
- 4th tab in Products page (alongside Úvěr, Hypotéka, Pojištění)
- Shows list of user's applications with status badges
- Color-coded: yellow=pending, green=approved/active, red=rejected, gray=cancelled
- Cancel button for pending applications

### 2. Calculator modals — submit to API
- Modify existing modals in LoanCalculatorPanel and MortgageCalculatorPanel
- Add modals to InsurancePanel (currently missing @onclick handlers)
- Modal shows parameter summary → "Odeslat žádost" button → POST to API
- Success feedback: "Žádost odeslána, čeká na schválení"

### 3. Banker management page — `/sprava`
- New Blazor component accessible to Banker role (nav: 📋 Správa)
- Table of pending product applications with applicant info
- Detail view with all parameters
- Approve/Reject buttons with optional note
- Banker = product/loan officer who reviews and approves applications

### 4. Admin page — `/admin`
- Admin = system administrator (logs, system health, user management)
- Admin can also see and approve/reject pending applications (as superuser)
- Separate from Banker — different nav item (⚙️ Admin)

### 5. Role-based UI restrictions (DONE)
- `AuthStateService` has: `IsAdmin`, `IsBanker`, `IsChild`, `IsStaff`
- Child role: "Produkty" hidden in SideNav and BottomNav
- Banker: sees "📋 Správa" nav item
- Admin: sees "⚙️ Admin" nav item
- All roles see "👤 Profil"
- Banker/Admin: show calculators but hide/disable submit buttons

---

## Technology Stack (consistent with project)

- ASP.NET Core 10 Minimal APIs
- EF Core 10 + PostgreSQL 16
- MediatR 14 (CQRS)
- FluentValidation 12.1
- Serilog + Kafka logging
- SharedKernel base classes (AggregateRoot, IRepository, IUnitOfWork)
