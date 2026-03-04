# Audit Log Implementation

This document describes the audit logging system implemented for the FairBank application. The system provides a centralized way to track sensitive administrative and user actions for security and compliance purposes.

## Architecture

### Backend (Identity Service)
The core of the audit log system resides in the **Identity Service**.

- **Entity**: `AuditLog` (located in `FairBank.Identity.Domain.Entities`) stores details about the action, the performing user, the target entity, and the timestamp.
- **Persistence**: 
    - `audit_logs` table in the `identity_service` PostgreSQL schema.
    - `AuditLogRepository` for data access.
    - Manual EF Core migration `20260304093500_AddAuditLogs` added to handle table creation in environments where automatic schema migration is used during startup.
- **Application Layer**:
    - `RecordAuditLogCommand`: A MediatR command for recording logs from any command handler.
    - `GetAuditLogsQuery`: A paginated query used by the admin interface to retrieve logs.
- **API Endpoint**: `GET /api/v1/users/admin/audit-logs` (requires Admin credentials).

### Frontend (Web Shared & Web)
- **API Client**: `FairBankApiClient` contains `GetAuditLogsAsync` to consume the paginated backend endpoint.
- **Models**: `AuditLogResponse` and `PagedAuditLogsResponse` define the data structure on the client side.
- **UI Components**:
    - `AdminLogs.razor`: A dedicated page in the Blazor Web project that displays a table of audit logs with pagination and human-readable timestamps.
    - **Navigation**: Accessible via the "Auditní Logy" button in the `Admin.razor` dashboard.

## Instrumented Actions
The following sensitive actions are currently logged:
- **Login**: Successful and failed login attempts.
- **Registration**: New user sign-ups.
- **Role Changes**: When an admin updates a user's role.
- **Account Management**: Activation and deactivation of user accounts.

## Troubleshooting Notes
- **Routing**: The endpoint is correctly mapped under `/api/v1/users/admin/audit-logs` and routed through the API Gateway.
- **Database**: Ensure that the `audit_logs` table exists. If a "500 Internal Server Error" occurs, check if migrations have been applied.
