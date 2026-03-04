# FairBank (Va-bank) project – Copilot instructions for AI agents

This repository implements a full‑stack digital banking system built in **.NET 10** with a micro‑services architecture. The goal of these instructions is to give an AI coding agent the context it would need to be productive without prior knowledge of the codebase.

---
## Big‑picture architecture 🏗️

* **Microservices**. Each service lives under `src/Services/<Service>` and follows a four‑layer hexagonal/clean layout: **Domain / Application / Infrastructure / Api**. Names are `FairBank.<Service>.<Layer>`.  
* **Shared kernel** at `src/FairBank.SharedKernel` contains base classes (e.g. `AggregateRoot`, `ValueObject`), port interfaces (`IUnitOfWork`, `IRepository`) and helper DI extensions.  
* **Frontend** is a Blazor WASM shell (`src/FairBank.Web`) with micro‑frontend modules in `FairBank.Web.*`. Shared models/client live in `FairBank.Web.Shared`.
* **API Gateway**: `src/FairBank.ApiGateway` uses YARP to proxy `/api/v1/*` to the appropriate service.
* **Persistence**:
  * *Identity* service uses **EF Core** against PostgreSQL with schema `identity_service`.  
  * *Accounts* uses **Marten event‑sourcing** in schema `accounts_service` (see `mt_*` tables).  
  * New services default to EF Core/CRUD; event sourcing is only used when the domain justifies it.
* **Docker Compose** sets up all services plus a primary/replica Postgres, Kafka and two web apps. Only `web-app` listens on host port 80.
* **Communication** between services is HTTP (typed clients are configured from environment variables such as `Services__IdentityApi`). The gateway hides ports from the frontend.

> 📂 See `docs/architecture_and_database.md` and the various `docs/plans/*.md` files for deeper background and examples.

---
## Conventions & patterns ✅

1. **CQRS + MediatR**: commands and queries live in the Application layer; handlers implement business logic and dispatch domain events. Validation uses FluentValidation classes.
2. **DTOs/responses** are simple POCOs returned by handlers and passed through endpoints.  
3. **Global query filters** apply soft‑delete (`is_deleted = false`) in EF models; do not bypass unless necessary.
4. **Program.cs** in each `*.Api` project is a minimal API bootstrap:
   ```csharp
   var builder = WebApplication.CreateBuilder(args);
   builder.Host.UseSerilog(...);
   builder.Services.Add<Service>Application();
   builder.Services.Add<Service>Infrastructure(cs);
   builder.Services.AddOpenApi();
   var app = builder.Build();
   app.Map<Some>Endpoints();
   app.MapGet("/health", ...);
   app.Run();
   public partial class Program; // needed for WebApplicationFactory tests
   ```
   Endpoint definitions are usually in `<Entity>Endpoints.cs` static classes.
5. **Dependency injection** extension methods (`DependencyInjection.cs` in Infrastructure) register EF/Marten, repositories, unit‑of‑work and anything else. Copy the existing pattern when adding a new service.
6. **Folder structure** within a service:
   * `Domain`: aggregates, value objects, domain events, enums.  
   * `Application`: commands/queries, handlers, DTOs, validators.  
   * `Infrastructure`: EF/Marten context, repository implementations, migrations, DI.  
   * `Api`: minimal API endpoints, `Program.cs`, Dockerfile.
7. **Naming**: interfaces prefixed with `I`; repository names `<Service>NameRepository`; handlers named `<Action>CommandHandler`.  
8. **Tests**: xUnit + FluentAssertions + NSubstitute. See existing tests for handler examples. Service‑level integration tests use `WebApplicationFactory` and sometimes Testcontainers for PostgreSQL. Use `[Fact]` or `[Theory]` where appropriate.

---
## Developer workflows 🛠

* **Build**: run `dotnet build FairBank.slnx` from repository root. All projects share versions via `Directory.Packages.props`.
* **Restore**: `dotnet restore` will use the centralized package props.
* **Run locally**:
  * Individual service: `dotnet run --project src/Services/Identity/FairBank.Identity.Api` (or pass `--urls`/env vars)  
  * Full stack: `docker-compose up --build` (root directory). The compose file supplies connection strings with `Search Path` set to service schema.  
  * Frontend: browse to `http://localhost` after `web-app` starts; use `ENABLE_CACHE=false` to disable static caching during development.
* **Database migrations**:
  * Identity uses EF migrations (`dotnet ef migrations add ... --project src/Services/Identity/FairBank.Identity.Infrastructure --output-dir Migrations` and `dotnet ef database update`).  
  * Accounts does not use migrations (Marten creates tables automatically).  
  * Remember to set `ASPNETCORE_ENVIRONMENT` to `Development` when running migrations against the Docker Postgres or point to the same connection string as the service in compose.
* **Testing**: run `dotnet test ./tests` or target a specific project. CI runs all unit tests; maintain 100‑% coverage for new features where possible.
* **Debugging**: attach debugger to a running service, or launch via Visual Studio/VS Code using the Csproj debug profile. `dotnet watch` is not configured but can be added if needed.
* **Docker chores**:
  * New service: add Dockerfile following existing pattern (copy csproj, shared kernel, service folder, `dotnet publish` step). Update `docker-compose.yml` and add schema grants in `docker/postgres/init.sql`.  
  * Healthchecks are defined in each Dockerfile and the API exposes `/health`.

---
## Service‑specific notes 📌

* **Identity**
  * Uses EF Core; soft‑delete, parent/child self‑referencing user for child accounts.
  * Role enum: `Client`, `Child`, `Banker`, `Admin`.  
  * Global query filter: `is_deleted = false`.

* **Accounts**
  * Uses Marten event sourcing. Aggregate snapshots are stored in `mt_doc_*` tables; the write model is a stream of events (`MoneyDeposited`, `AccountCreated`, etc.).  
  * Service communicates with Identity via HTTP client configured at `Services__IdentityApi`.

* **Other services** (Payments, Products, Chat, Cards, Notifications, Documents, etc.)
  * Follow the EF Core/CRUD template; Documents adds document‑generation libraries and a dedicated HTTP client for accounts.  
  * Pay attention to cross‑service dependencies; environment variables are read in `Program.cs` and injected into typed clients.
  * Documents service handles PDF/Excel/DOCX export of statements and contracts; its API endpoint is `/api/v1/documents/statements`.

* **Frontend modules**
  * Add new tabs/pages by creating a Razor class library under `FairBank.Web.*`. Reference the assembly in `src/FairBank.Web/App.razor`.  
  * Shared components live in `FairBank.Web.Shared` (e.g. `ApiClient`, `AuthService`).  
  * Use `@inject AuthService Auth` to access authentication state; session is kept via cookies.

---
## Integration & external dependencies⛓️

* **PostgreSQL** with two schemas in a single database; connection strings set via compose (search path).  
* **Kafka** is included for future event‑driven features; some services produce/consume messages using `Confluent.Kafka` package.  
* **YARP** configuration is stored in `src/FairBank.ApiGateway/appsettings.json`; modifications are rare but necessary when adding new services or paths.  
  - When a new service is added (e.g. `Documents`), remember to add a route/cluster mapping and adjust `docker-compose.yml` accordingly.
* **Third‑party packages** are centrally versioned; upgrade here and run `dotnet restore` to propagate.

---
## What to read first 📚

1. `docs/architecture_and_database.md` – full explanation of services, persistence and data model.  
2. `agent-instructions/PROJECT.md` – hand‑written high‑level overview and future plans.  
3. Any `docs/plans/*.md` file for feature‑specific guidance; these contain numerous code snippets that serve as patterns.
4. A sample service (Identity or Accounts) and its corresponding tests—everything else is a copy of the same pattern.

---

> 💬 **Feedback request:** please review this guidance and let me know if any project‑specific practice or workflow is missing or unclear. I can expand the instructions as needed.