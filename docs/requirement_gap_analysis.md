# FairBank: Project Status & Gap Analysis

This document provides a prioritized review of the FairBank project status, identifying completed features and remaining gaps required to make the application function like a production-ready bank.

## Status Overview

FairBank has a solid microservices foundation with Hexagonal Architecture, CQRS, and Event Sourcing (Marten). Most core banking features (Accounts, Payments, Cards, 2FA, Chat) are implemented at both the backend and frontend levels.

---

## Priority 1: Mission Critical
*These features are essential for the system to safely and legally operate as a real financial institution.*

### 1.1 From "Soft Auth" to "Hard Auth"
* **Status**: ⚠️ **HIGH RISK**
* **Issue**: Current documentation and code indicate that while the frontend sends tokens, the backend does not validate them (`.RequireAuthorization()` is missing from most endpoints).
* **Requirement**: Implement JWT/Session validation on all API Gateway routes and backend services. Enforce Role-Based Access Control (RBAC).

### 1.2 Audit Logging & Compliance
* **Status**: ❌ **MISSING**
* **Requirement**: For a bank, every action (login, transfer, limit change) must be recorded in a tamper-proof audit log. This is required for financial regulations.

### 1.3 Background Task Processing
* **Status**: ❌ **MISSING**
* **Requirement**: Standing orders and Savings rules are stored in the database but have no "engine" to execute them.
* **Tooling**: Implement a background worker (e.g., Hangfire or Quartz.NET) to process recurring payments and interest calculations.

### 1.4 Database Schema Integrity
* **Status**: ⚠️ **PARTIAL**
* **Requirement**: Recent features like *TOTP* and *Device Management* need EF Core migrations applied to the production/docker databases.

---

## Priority 2: Core Banking UX
*Features needed for a modern, functional daily banking experience.*

### 2.1 Notifications Triggering
* **Status**: 🟠 **SERVICE EXISTS, TRIGGERS MISSING**
* **Requirement**: The `NotificationService` is built, but it needs to be integrated into the `PaymentService` (send receipt on transfer) and `IdentityService` (send alert on new device login).

### 2.2 External Payments Simulation
* **Status**: 🟠 **INTERNAL ONLY**
* **Requirement**: Implement a gateway to simulate payments to external IBANs or account numbers outside the FairBank system.

### 2.3 Interest Accrual
* **Status**: ❌ **MISSING**
* **Requirement**: Savings accounts should automatically accrue interest. This requires a periodic job to calculate and "Deposit" interest based on the current balance.

---

## Priority 3: Feature Completion
*Expanding the bank's product offering.*

### 3.1 Loans & Insurance
* **Status**: 🟠 **PARTIAL / UI SHELLS**
* **Requirement**: Implement the full lifecycle for loan applications (Application -> Banker Review -> Disbursement -> Amortization).

### 3.2 QR Payments
* **Status**: ❌ **MISSING**
* **Requirement**: Support for generating and scanning QR codes for payments (e.g., EPC-QR standard).

### 3.3 Investment Realism
* **Status**: 🟠 **SIMULATED**
* **Requirement**: Integrate a simulated "Market Price" feed to update `Investment` values automatically over time, rather than relying on manual updates.

---

## Summary of Completed Features (FYI)
- ✅ **Identity**: Register, Login, 2FA (TOTP), Device Tracking, Profile.
- ✅ **Accounts**: Multi-currency, Balances, History (Event-Sourced).
- ✅ **Payments**: Domestic transfers, Standing orders, Limits, Categories.
- ✅ **Cards**: Virtual/Physical issuance, Freeze/Unfreeze, Limit control.
- ✅ **Family**: Parent/Child relationship, Spending limits, Transaction Approval.
- ✅ **Support**: Real-time Chat with persistent history and Banker views.
- ✅ **Admin**: User management, Role control, Paginated views.
