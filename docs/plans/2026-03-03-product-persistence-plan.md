# Product Persistence Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a new Products microservice with persistence for loan/mortgage/insurance applications, approval workflow by Banker/Admin, and frontend integration.

**Architecture:** New `FairBank.Products` service following Clean Architecture + CQRS pattern (same as Payments). EF Core with PostgreSQL `products_service` schema, MediatR, Minimal APIs. Frontend modals submit to API, new "Moje produkty" tab shows user's applications, Banker gets `/sprava` page for approvals.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL 16, MediatR 14, FluentValidation 12.1, Serilog, YARP 2.3

**Design doc:** `docs/plans/2026-03-03-product-persistence-design.md`

---

### Task 1: Scaffold Products Domain Layer

**Files:**
- Create: `src/Services/Products/FairBank.Products.Domain/FairBank.Products.Domain.csproj`
- Create: `src/Services/Products/FairBank.Products.Domain/Enums/ProductType.cs`
- Create: `src/Services/Products/FairBank.Products.Domain/Enums/ApplicationStatus.cs`
- Create: `src/Services/Products/FairBank.Products.Domain/Entities/ProductApplication.cs`
- Create: `src/Services/Products/FairBank.Products.Domain/Repositories/IProductApplicationRepository.cs`

**Step 1: Create Domain csproj**

```xml
<!-- src/Services/Products/FairBank.Products.Domain/FairBank.Products.Domain.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="../../../FairBank.SharedKernel/FairBank.SharedKernel.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Create enums**

```csharp
// src/Services/Products/FairBank.Products.Domain/Enums/ProductType.cs
namespace FairBank.Products.Domain.Enums;

public enum ProductType
{
    PersonalLoan = 0,
    Mortgage = 1,
    TravelInsurance = 2,
    PropertyInsurance = 3,
    LifeInsurance = 4,
    PaymentProtection = 5
}
```

```csharp
// src/Services/Products/FairBank.Products.Domain/Enums/ApplicationStatus.cs
namespace FairBank.Products.Domain.Enums;

public enum ApplicationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Active = 3,
    Cancelled = 4,
    Completed = 5
}
```

**Step 3: Create ProductApplication aggregate**

```csharp
// src/Services/Products/FairBank.Products.Domain/Entities/ProductApplication.cs
using FairBank.Products.Domain.Enums;
using FairBank.SharedKernel.Domain;

namespace FairBank.Products.Domain.Entities;

public sealed class ProductApplication : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public ProductType ProductType { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public string Parameters { get; private set; } = string.Empty;
    public decimal MonthlyPayment { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public string? Note { get; private set; }

    private ProductApplication() { } // EF Core

    public static ProductApplication Create(
        Guid userId,
        ProductType productType,
        string parameters,
        decimal monthlyPayment)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.");
        if (string.IsNullOrWhiteSpace(parameters)) throw new ArgumentException("Parameters are required.");
        if (monthlyPayment < 0) throw new ArgumentException("MonthlyPayment cannot be negative.");

        return new ProductApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductType = productType,
            Status = ApplicationStatus.Pending,
            Parameters = parameters,
            MonthlyPayment = monthlyPayment,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve(Guid reviewerId, string? note = null)
    {
        if (Status != ApplicationStatus.Pending)
            throw new InvalidOperationException($"Cannot approve application in status {Status}.");

        Status = ApplicationStatus.Active;
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = reviewerId;
        Note = note;
    }

    public void Reject(Guid reviewerId, string? note = null)
    {
        if (Status != ApplicationStatus.Pending)
            throw new InvalidOperationException($"Cannot reject application in status {Status}.");

        Status = ApplicationStatus.Rejected;
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = reviewerId;
        Note = note;
    }

    public void Cancel()
    {
        if (Status != ApplicationStatus.Pending)
            throw new InvalidOperationException($"Cannot cancel application in status {Status}.");

        Status = ApplicationStatus.Cancelled;
    }
}
```

**Step 4: Create repository interface**

```csharp
// src/Services/Products/FairBank.Products.Domain/Repositories/IProductApplicationRepository.cs
using FairBank.Products.Domain.Entities;
using FairBank.SharedKernel.Domain;

namespace FairBank.Products.Domain.Repositories;

public interface IProductApplicationRepository : IRepository<ProductApplication, Guid>
{
    Task<IReadOnlyList<ProductApplication>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductApplication>> GetPendingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductApplication>> GetAllAsync(int limit = 100, CancellationToken ct = default);
}
```

**Step 5: Commit**

```bash
git add src/Services/Products/FairBank.Products.Domain/
git commit -m "feat(products): scaffold domain layer with ProductApplication aggregate"
```

---

### Task 2: Scaffold Products Infrastructure Layer

**Files:**
- Create: `src/Services/Products/FairBank.Products.Infrastructure/FairBank.Products.Infrastructure.csproj`
- Create: `src/Services/Products/FairBank.Products.Infrastructure/Persistence/ProductsDbContext.cs`
- Create: `src/Services/Products/FairBank.Products.Infrastructure/Persistence/Configurations/ProductApplicationConfiguration.cs`
- Create: `src/Services/Products/FairBank.Products.Infrastructure/Persistence/Repositories/ProductApplicationRepository.cs`
- Create: `src/Services/Products/FairBank.Products.Infrastructure/DependencyInjection.cs`

**Step 1: Create Infrastructure csproj**

```xml
<!-- src/Services/Products/FairBank.Products.Infrastructure/FairBank.Products.Infrastructure.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../FairBank.Products.Domain/FairBank.Products.Domain.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Create DbContext**

```csharp
// src/Services/Products/FairBank.Products.Infrastructure/Persistence/ProductsDbContext.cs
using FairBank.Products.Domain.Entities;
using FairBank.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Products.Infrastructure.Persistence;

public sealed class ProductsDbContext(DbContextOptions<ProductsDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<ProductApplication> ProductApplications => Set<ProductApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("products_service");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductsDbContext).Assembly);
    }
}
```

**Step 3: Create EF Configuration**

```csharp
// src/Services/Products/FairBank.Products.Infrastructure/Persistence/Configurations/ProductApplicationConfiguration.cs
using FairBank.Products.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Products.Infrastructure.Persistence.Configurations;

public sealed class ProductApplicationConfiguration : IEntityTypeConfiguration<ProductApplication>
{
    public void Configure(EntityTypeBuilder<ProductApplication> builder)
    {
        builder.ToTable("product_applications");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.UserId).IsRequired();
        builder.Property(p => p.ProductType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Parameters).HasColumnType("text").IsRequired();
        builder.Property(p => p.MonthlyPayment).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.Note).HasMaxLength(500);

        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.CreatedAt).IsDescending();
    }
}
```

**Step 4: Create Repository**

```csharp
// src/Services/Products/FairBank.Products.Infrastructure/Persistence/Repositories/ProductApplicationRepository.cs
using FairBank.Products.Domain.Entities;
using FairBank.Products.Domain.Enums;
using FairBank.Products.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Products.Infrastructure.Persistence.Repositories;

public sealed class ProductApplicationRepository(ProductsDbContext context) : IProductApplicationRepository
{
    public async Task<ProductApplication?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.ProductApplications.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(ProductApplication aggregate, CancellationToken ct = default)
        => await context.ProductApplications.AddAsync(aggregate, ct);

    public Task UpdateAsync(ProductApplication aggregate, CancellationToken ct = default)
    {
        context.ProductApplications.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ProductApplication>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await context.ProductApplications
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProductApplication>> GetPendingAsync(CancellationToken ct = default)
        => await context.ProductApplications
            .Where(p => p.Status == ApplicationStatus.Pending)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProductApplication>> GetAllAsync(int limit = 100, CancellationToken ct = default)
        => await context.ProductApplications
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
}
```

**Step 5: Create DependencyInjection**

```csharp
// src/Services/Products/FairBank.Products.Infrastructure/DependencyInjection.cs
using FairBank.Products.Domain.Repositories;
using FairBank.Products.Infrastructure.Persistence;
using FairBank.Products.Infrastructure.Persistence.Repositories;
using FairBank.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Products.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ProductsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "products_service");
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            }));

        services.AddScoped<IProductApplicationRepository, ProductApplicationRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ProductsDbContext>());

        return services;
    }
}
```

**Step 6: Commit**

```bash
git add src/Services/Products/FairBank.Products.Infrastructure/
git commit -m "feat(products): scaffold infrastructure layer with EF Core persistence"
```

---

### Task 3: Scaffold Products Application Layer

**Files:**
- Create: `src/Services/Products/FairBank.Products.Application/FairBank.Products.Application.csproj`
- Create: `src/Services/Products/FairBank.Products.Application/DependencyInjection.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Dtos/ProductApplicationResponse.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Commands/SubmitApplication/SubmitApplicationCommand.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Commands/SubmitApplication/SubmitApplicationCommandHandler.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Commands/SubmitApplication/SubmitApplicationCommandValidator.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Commands/ApproveApplication/ApproveApplicationCommand.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Commands/ApproveApplication/ApproveApplicationCommandHandler.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Commands/RejectApplication/RejectApplicationCommand.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Commands/RejectApplication/RejectApplicationCommandHandler.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Commands/CancelApplication/CancelApplicationCommand.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Commands/CancelApplication/CancelApplicationCommandHandler.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Queries/GetUserApplications/GetUserApplicationsQuery.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Queries/GetUserApplications/GetUserApplicationsQueryHandler.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Queries/GetPendingApplications/GetPendingApplicationsQuery.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Queries/GetPendingApplications/GetPendingApplicationsQueryHandler.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Queries/GetApplicationById/GetApplicationByIdQuery.cs`
- Create: `src/Services/Products/FairBank.Products.Application/Queries/GetApplicationById/GetApplicationByIdQueryHandler.cs`

**Step 1: Create Application csproj**

```xml
<!-- src/Services/Products/FairBank.Products.Application/FairBank.Products.Application.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="MediatR" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../FairBank.Products.Domain/FairBank.Products.Domain.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Create DependencyInjection**

```csharp
// src/Services/Products/FairBank.Products.Application/DependencyInjection.cs
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FairBank.Products.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProductsApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
```

**Step 3: Create DTO**

```csharp
// src/Services/Products/FairBank.Products.Application/Dtos/ProductApplicationResponse.cs
namespace FairBank.Products.Application.Dtos;

public sealed record ProductApplicationResponse(
    Guid Id,
    Guid UserId,
    string ProductType,
    string Status,
    string Parameters,
    decimal MonthlyPayment,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    Guid? ReviewedBy,
    string? Note);
```

**Step 4: Create SubmitApplication command + handler + validator**

```csharp
// src/Services/Products/FairBank.Products.Application/Commands/SubmitApplication/SubmitApplicationCommand.cs
using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Commands.SubmitApplication;

public sealed record SubmitApplicationCommand(
    Guid UserId,
    string ProductType,
    string Parameters,
    decimal MonthlyPayment) : IRequest<ProductApplicationResponse>;
```

```csharp
// src/Services/Products/FairBank.Products.Application/Commands/SubmitApplication/SubmitApplicationCommandHandler.cs
using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Enums;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Domain;
using MediatR;

namespace FairBank.Products.Application.Commands.SubmitApplication;

public sealed class SubmitApplicationCommandHandler(
    IProductApplicationRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<SubmitApplicationCommand, ProductApplicationResponse>
{
    public async Task<ProductApplicationResponse> Handle(SubmitApplicationCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ProductType>(request.ProductType, true, out var productType))
            throw new ArgumentException($"Invalid product type: {request.ProductType}");

        var application = Domain.Entities.ProductApplication.Create(
            request.UserId,
            productType,
            request.Parameters,
            request.MonthlyPayment);

        await repository.AddAsync(application, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(application);
    }

    private static ProductApplicationResponse MapToResponse(Domain.Entities.ProductApplication a) => new(
        a.Id, a.UserId, a.ProductType.ToString(), a.Status.ToString(),
        a.Parameters, a.MonthlyPayment, a.CreatedAt,
        a.ReviewedAt, a.ReviewedBy, a.Note);
}
```

```csharp
// src/Services/Products/FairBank.Products.Application/Commands/SubmitApplication/SubmitApplicationCommandValidator.cs
using FluentValidation;

namespace FairBank.Products.Application.Commands.SubmitApplication;

public sealed class SubmitApplicationCommandValidator : AbstractValidator<SubmitApplicationCommand>
{
    public SubmitApplicationCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ProductType).NotEmpty();
        RuleFor(x => x.Parameters).NotEmpty();
        RuleFor(x => x.MonthlyPayment).GreaterThanOrEqualTo(0);
    }
}
```

**Step 5: Create ApproveApplication command + handler**

```csharp
// src/Services/Products/FairBank.Products.Application/Commands/ApproveApplication/ApproveApplicationCommand.cs
using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Commands.ApproveApplication;

public sealed record ApproveApplicationCommand(
    Guid ApplicationId,
    Guid ReviewerId,
    string? Note = null) : IRequest<ProductApplicationResponse>;
```

```csharp
// src/Services/Products/FairBank.Products.Application/Commands/ApproveApplication/ApproveApplicationCommandHandler.cs
using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Domain;
using MediatR;

namespace FairBank.Products.Application.Commands.ApproveApplication;

public sealed class ApproveApplicationCommandHandler(
    IProductApplicationRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<ApproveApplicationCommand, ProductApplicationResponse>
{
    public async Task<ProductApplicationResponse> Handle(ApproveApplicationCommand request, CancellationToken ct)
    {
        var application = await repository.GetByIdAsync(request.ApplicationId, ct)
            ?? throw new InvalidOperationException("Application not found.");

        application.Approve(request.ReviewerId, request.Note);
        await repository.UpdateAsync(application, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ProductApplicationResponse(
            application.Id, application.UserId, application.ProductType.ToString(),
            application.Status.ToString(), application.Parameters, application.MonthlyPayment,
            application.CreatedAt, application.ReviewedAt, application.ReviewedBy, application.Note);
    }
}
```

**Step 6: Create RejectApplication command + handler**

```csharp
// src/Services/Products/FairBank.Products.Application/Commands/RejectApplication/RejectApplicationCommand.cs
using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Commands.RejectApplication;

public sealed record RejectApplicationCommand(
    Guid ApplicationId,
    Guid ReviewerId,
    string? Note = null) : IRequest<ProductApplicationResponse>;
```

```csharp
// src/Services/Products/FairBank.Products.Application/Commands/RejectApplication/RejectApplicationCommandHandler.cs
using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Domain;
using MediatR;

namespace FairBank.Products.Application.Commands.RejectApplication;

public sealed class RejectApplicationCommandHandler(
    IProductApplicationRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<RejectApplicationCommand, ProductApplicationResponse>
{
    public async Task<ProductApplicationResponse> Handle(RejectApplicationCommand request, CancellationToken ct)
    {
        var application = await repository.GetByIdAsync(request.ApplicationId, ct)
            ?? throw new InvalidOperationException("Application not found.");

        application.Reject(request.ReviewerId, request.Note);
        await repository.UpdateAsync(application, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ProductApplicationResponse(
            application.Id, application.UserId, application.ProductType.ToString(),
            application.Status.ToString(), application.Parameters, application.MonthlyPayment,
            application.CreatedAt, application.ReviewedAt, application.ReviewedBy, application.Note);
    }
}
```

**Step 7: Create CancelApplication command + handler**

```csharp
// src/Services/Products/FairBank.Products.Application/Commands/CancelApplication/CancelApplicationCommand.cs
using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Commands.CancelApplication;

public sealed record CancelApplicationCommand(
    Guid ApplicationId,
    Guid UserId) : IRequest<ProductApplicationResponse>;
```

```csharp
// src/Services/Products/FairBank.Products.Application/Commands/CancelApplication/CancelApplicationCommandHandler.cs
using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using FairBank.SharedKernel.Domain;
using MediatR;

namespace FairBank.Products.Application.Commands.CancelApplication;

public sealed class CancelApplicationCommandHandler(
    IProductApplicationRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelApplicationCommand, ProductApplicationResponse>
{
    public async Task<ProductApplicationResponse> Handle(CancelApplicationCommand request, CancellationToken ct)
    {
        var application = await repository.GetByIdAsync(request.ApplicationId, ct)
            ?? throw new InvalidOperationException("Application not found.");

        if (application.UserId != request.UserId)
            throw new InvalidOperationException("Only the applicant can cancel their application.");

        application.Cancel();
        await repository.UpdateAsync(application, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ProductApplicationResponse(
            application.Id, application.UserId, application.ProductType.ToString(),
            application.Status.ToString(), application.Parameters, application.MonthlyPayment,
            application.CreatedAt, application.ReviewedAt, application.ReviewedBy, application.Note);
    }
}
```

**Step 8: Create queries**

```csharp
// src/Services/Products/FairBank.Products.Application/Queries/GetUserApplications/GetUserApplicationsQuery.cs
using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Queries.GetUserApplications;

public sealed record GetUserApplicationsQuery(Guid UserId) : IRequest<IReadOnlyList<ProductApplicationResponse>>;
```

```csharp
// src/Services/Products/FairBank.Products.Application/Queries/GetUserApplications/GetUserApplicationsQueryHandler.cs
using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using MediatR;

namespace FairBank.Products.Application.Queries.GetUserApplications;

public sealed class GetUserApplicationsQueryHandler(
    IProductApplicationRepository repository) : IRequestHandler<GetUserApplicationsQuery, IReadOnlyList<ProductApplicationResponse>>
{
    public async Task<IReadOnlyList<ProductApplicationResponse>> Handle(GetUserApplicationsQuery request, CancellationToken ct)
    {
        var applications = await repository.GetByUserIdAsync(request.UserId, ct);
        return applications.Select(a => new ProductApplicationResponse(
            a.Id, a.UserId, a.ProductType.ToString(), a.Status.ToString(),
            a.Parameters, a.MonthlyPayment, a.CreatedAt,
            a.ReviewedAt, a.ReviewedBy, a.Note)).ToList();
    }
}
```

```csharp
// src/Services/Products/FairBank.Products.Application/Queries/GetPendingApplications/GetPendingApplicationsQuery.cs
using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Queries.GetPendingApplications;

public sealed record GetPendingApplicationsQuery() : IRequest<IReadOnlyList<ProductApplicationResponse>>;
```

```csharp
// src/Services/Products/FairBank.Products.Application/Queries/GetPendingApplications/GetPendingApplicationsQueryHandler.cs
using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using MediatR;

namespace FairBank.Products.Application.Queries.GetPendingApplications;

public sealed class GetPendingApplicationsQueryHandler(
    IProductApplicationRepository repository) : IRequestHandler<GetPendingApplicationsQuery, IReadOnlyList<ProductApplicationResponse>>
{
    public async Task<IReadOnlyList<ProductApplicationResponse>> Handle(GetPendingApplicationsQuery request, CancellationToken ct)
    {
        var applications = await repository.GetPendingAsync(ct);
        return applications.Select(a => new ProductApplicationResponse(
            a.Id, a.UserId, a.ProductType.ToString(), a.Status.ToString(),
            a.Parameters, a.MonthlyPayment, a.CreatedAt,
            a.ReviewedAt, a.ReviewedBy, a.Note)).ToList();
    }
}
```

```csharp
// src/Services/Products/FairBank.Products.Application/Queries/GetApplicationById/GetApplicationByIdQuery.cs
using FairBank.Products.Application.Dtos;
using MediatR;

namespace FairBank.Products.Application.Queries.GetApplicationById;

public sealed record GetApplicationByIdQuery(Guid ApplicationId) : IRequest<ProductApplicationResponse?>;
```

```csharp
// src/Services/Products/FairBank.Products.Application/Queries/GetApplicationById/GetApplicationByIdQueryHandler.cs
using FairBank.Products.Application.Dtos;
using FairBank.Products.Domain.Repositories;
using MediatR;

namespace FairBank.Products.Application.Queries.GetApplicationById;

public sealed class GetApplicationByIdQueryHandler(
    IProductApplicationRepository repository) : IRequestHandler<GetApplicationByIdQuery, ProductApplicationResponse?>
{
    public async Task<ProductApplicationResponse?> Handle(GetApplicationByIdQuery request, CancellationToken ct)
    {
        var a = await repository.GetByIdAsync(request.ApplicationId, ct);
        if (a is null) return null;

        return new ProductApplicationResponse(
            a.Id, a.UserId, a.ProductType.ToString(), a.Status.ToString(),
            a.Parameters, a.MonthlyPayment, a.CreatedAt,
            a.ReviewedAt, a.ReviewedBy, a.Note);
    }
}
```

**Step 9: Commit**

```bash
git add src/Services/Products/FairBank.Products.Application/
git commit -m "feat(products): scaffold application layer with CQRS commands and queries"
```

---

### Task 4: Scaffold Products API Layer

**Files:**
- Create: `src/Services/Products/FairBank.Products.Api/FairBank.Products.Api.csproj`
- Create: `src/Services/Products/FairBank.Products.Api/Program.cs`
- Create: `src/Services/Products/FairBank.Products.Api/Endpoints/ProductApplicationEndpoints.cs`
- Create: `src/Services/Products/FairBank.Products.Api/appsettings.json`
- Create: `src/Services/Products/FairBank.Products.Api/appsettings.Development.json`
- Create: `src/Services/Products/FairBank.Products.Api/Dockerfile`

**Step 1: Create Api csproj**

```xml
<!-- src/Services/Products/FairBank.Products.Api/FairBank.Products.Api.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Scalar.AspNetCore" />
    <PackageReference Include="Serilog.AspNetCore" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../FairBank.Products.Application/FairBank.Products.Application.csproj" />
    <ProjectReference Include="../FairBank.Products.Infrastructure/FairBank.Products.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Create Program.cs**

```csharp
// src/Services/Products/FairBank.Products.Api/Program.cs
using FairBank.Products.Application;
using FairBank.Products.Infrastructure;
using FairBank.Products.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.AddProductsApplication();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

builder.Services.AddProductsInfrastructure(connectionString);
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();

app.MapProductApplicationEndpoints();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "Products" }))
    .WithTags("Health");

app.Run();

public partial class Program;
```

**Step 3: Create Endpoints**

```csharp
// src/Services/Products/FairBank.Products.Api/Endpoints/ProductApplicationEndpoints.cs
using FairBank.Products.Application.Commands.ApproveApplication;
using FairBank.Products.Application.Commands.CancelApplication;
using FairBank.Products.Application.Commands.RejectApplication;
using FairBank.Products.Application.Commands.SubmitApplication;
using FairBank.Products.Application.Queries.GetApplicationById;
using FairBank.Products.Application.Queries.GetPendingApplications;
using FairBank.Products.Application.Queries.GetUserApplications;
using MediatR;

namespace FairBank.Products.Api.Endpoints;

public static class ProductApplicationEndpoints
{
    public static void MapProductApplicationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/products/applications").WithTags("Product Applications");

        group.MapPost("/", async (SubmitApplicationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return Results.Created($"/api/v1/products/applications/{result.Id}", result);
        })
        .WithName("SubmitApplication")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        group.MapGet("/user/{userId:guid}", async (Guid userId, ISender sender) =>
        {
            var result = await sender.Send(new GetUserApplicationsQuery(userId));
            return Results.Ok(result);
        })
        .WithName("GetUserApplications")
        .Produces(StatusCodes.Status200OK);

        group.MapGet("/pending", async (ISender sender) =>
        {
            var result = await sender.Send(new GetPendingApplicationsQuery());
            return Results.Ok(result);
        })
        .WithName("GetPendingApplications")
        .Produces(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetApplicationByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetApplicationById")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/approve", async (Guid id, ApproveApplicationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { ApplicationId = id });
            return Results.Ok(result);
        })
        .WithName("ApproveApplication")
        .Produces(StatusCodes.Status200OK);

        group.MapPut("/{id:guid}/reject", async (Guid id, RejectApplicationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { ApplicationId = id });
            return Results.Ok(result);
        })
        .WithName("RejectApplication")
        .Produces(StatusCodes.Status200OK);

        group.MapPut("/{id:guid}/cancel", async (Guid id, CancelApplicationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { ApplicationId = id });
            return Results.Ok(result);
        })
        .WithName("CancelApplication")
        .Produces(StatusCodes.Status200OK);
    }
}
```

**Step 4: Create config files**

```json
// src/Services/Products/FairBank.Products.Api/appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  }
}
```

```json
// src/Services/Products/FairBank.Products.Api/appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=products_service"
  }
}
```

**Step 5: Create Dockerfile**

```dockerfile
# src/Services/Products/FairBank.Products.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/FairBank.SharedKernel/FairBank.SharedKernel.csproj src/FairBank.SharedKernel/
COPY src/Services/Products/FairBank.Products.Domain/FairBank.Products.Domain.csproj src/Services/Products/FairBank.Products.Domain/
COPY src/Services/Products/FairBank.Products.Application/FairBank.Products.Application.csproj src/Services/Products/FairBank.Products.Application/
COPY src/Services/Products/FairBank.Products.Infrastructure/FairBank.Products.Infrastructure.csproj src/Services/Products/FairBank.Products.Infrastructure/
COPY src/Services/Products/FairBank.Products.Api/FairBank.Products.Api.csproj src/Services/Products/FairBank.Products.Api/
RUN dotnet restore src/Services/Products/FairBank.Products.Api/FairBank.Products.Api.csproj

COPY src/ src/
RUN dotnet publish src/Services/Products/FairBank.Products.Api/FairBank.Products.Api.csproj -c Release -o /app/publish

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.Products.Api.dll"]
```

**Step 6: Commit**

```bash
git add src/Services/Products/FairBank.Products.Api/
git commit -m "feat(products): scaffold API layer with minimal endpoints and Dockerfile"
```

---

### Task 5: Wire Products Service into Infrastructure

**Files:**
- Modify: `FairBank.slnx` — add 4 new projects
- Modify: `docker-compose.yml` — add products-api service
- Modify: `docker/postgres/init.sql` — add products_service schema
- Modify: `src/ApiGateway/FairBank.ApiGateway/appsettings.json` — add YARP route

**Step 1: Add projects to solution**

Run:
```bash
dotnet sln FairBank.slnx add \
  src/Services/Products/FairBank.Products.Domain/FairBank.Products.Domain.csproj \
  src/Services/Products/FairBank.Products.Application/FairBank.Products.Application.csproj \
  src/Services/Products/FairBank.Products.Infrastructure/FairBank.Products.Infrastructure.csproj \
  src/Services/Products/FairBank.Products.Api/FairBank.Products.Api.csproj
```

**Step 2: Add to docker-compose.yml**

Add this service block after `chat-api` (before `kafka`):

```yaml
  products-api:
    build:
      context: .
      dockerfile: src/Services/Products/FairBank.Products.Api/Dockerfile
    container_name: fairbank-products-api
    expose:
      - "8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Host=postgres-primary;Port=5432;Database=fairbank;Username=fairbank_app;Password=fairbank_app_2026;Search Path=products_service"
    depends_on:
      postgres-primary:
        condition: service_healthy
    networks:
      - backend
    restart: unless-stopped
```

Also add `products-api` to the `api-gateway` depends_on list.

**Step 3: Add schema to init.sql**

Add after the existing `chat_service` schema block:

```sql
CREATE SCHEMA IF NOT EXISTS products_service;
GRANT ALL PRIVILEGES ON SCHEMA products_service TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA products_service GRANT ALL ON TABLES TO fairbank_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA products_service GRANT ALL ON SEQUENCES TO fairbank_app;
```

**Step 4: Add YARP route**

Add to `ReverseProxy.Routes` in API Gateway appsettings.json:

```json
"products-route": {
  "ClusterId": "products-cluster",
  "Match": {
    "Path": "/api/v1/products/{**catch-all}"
  }
}
```

Add to `ReverseProxy.Clusters`:

```json
"products-cluster": {
  "Destinations": {
    "products-api": {
      "Address": "http://products-api:8080"
    }
  }
}
```

**Step 5: Verify build**

```bash
dotnet build FairBank.slnx
```

**Step 6: Commit**

```bash
git add FairBank.slnx docker-compose.yml docker/postgres/init.sql src/ApiGateway/
git commit -m "feat(products): wire service into Docker, YARP gateway, and solution"
```

---

### Task 6: Add Frontend DTO and API Client Methods

**Files:**
- Create: `src/FairBank.Web.Shared/Models/ProductApplicationDto.cs`
- Modify: `src/FairBank.Web.Shared/Services/IFairBankApi.cs` — add product methods
- Modify: `src/FairBank.Web.Shared/Services/FairBankApiClient.cs` — implement product methods

**Step 1: Create DTO**

```csharp
// src/FairBank.Web.Shared/Models/ProductApplicationDto.cs
namespace FairBank.Web.Shared.Models;

public sealed record ProductApplicationDto(
    Guid Id,
    Guid UserId,
    string ProductType,
    string Status,
    string Parameters,
    decimal MonthlyPayment,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    Guid? ReviewedBy,
    string? Note);
```

**Step 2: Add to IFairBankApi**

Add at the end of the interface:

```csharp
// Product applications
Task<ProductApplicationDto> SubmitProductApplicationAsync(Guid userId, string productType, string parameters, decimal monthlyPayment);
Task<List<ProductApplicationDto>> GetUserApplicationsAsync(Guid userId);
Task<List<ProductApplicationDto>> GetPendingApplicationsAsync();
Task<ProductApplicationDto> ApproveApplicationAsync(Guid applicationId, Guid reviewerId, string? note = null);
Task<ProductApplicationDto> RejectApplicationAsync(Guid applicationId, Guid reviewerId, string? note = null);
Task<ProductApplicationDto> CancelApplicationAsync(Guid applicationId, Guid userId);
```

**Step 3: Implement in FairBankApiClient**

Add at the end of the class:

```csharp
// ── Product applications ──────────────────────────────────
public async Task<ProductApplicationDto> SubmitProductApplicationAsync(Guid userId, string productType, string parameters, decimal monthlyPayment)
{
    var response = await http.PostAsJsonAsync("api/v1/products/applications",
        new { UserId = userId, ProductType = productType, Parameters = parameters, MonthlyPayment = monthlyPayment });
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<ProductApplicationDto>())!;
}

public async Task<List<ProductApplicationDto>> GetUserApplicationsAsync(Guid userId)
{
    return await http.GetFromJsonAsync<List<ProductApplicationDto>>($"api/v1/products/applications/user/{userId}") ?? [];
}

public async Task<List<ProductApplicationDto>> GetPendingApplicationsAsync()
{
    return await http.GetFromJsonAsync<List<ProductApplicationDto>>("api/v1/products/applications/pending") ?? [];
}

public async Task<ProductApplicationDto> ApproveApplicationAsync(Guid applicationId, Guid reviewerId, string? note = null)
{
    var response = await http.PutAsJsonAsync($"api/v1/products/applications/{applicationId}/approve",
        new { ApplicationId = applicationId, ReviewerId = reviewerId, Note = note });
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<ProductApplicationDto>())!;
}

public async Task<ProductApplicationDto> RejectApplicationAsync(Guid applicationId, Guid reviewerId, string? note = null)
{
    var response = await http.PutAsJsonAsync($"api/v1/products/applications/{applicationId}/reject",
        new { ApplicationId = applicationId, ReviewerId = reviewerId, Note = note });
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<ProductApplicationDto>())!;
}

public async Task<ProductApplicationDto> CancelApplicationAsync(Guid applicationId, Guid userId)
{
    var response = await http.PutAsJsonAsync($"api/v1/products/applications/{applicationId}/cancel",
        new { ApplicationId = applicationId, UserId = userId });
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<ProductApplicationDto>())!;
}
```

**Step 4: Commit**

```bash
git add src/FairBank.Web.Shared/
git commit -m "feat(products): add frontend DTO and API client methods for product applications"
```

---

### Task 7: Update Calculator Modals to Submit Applications

**Files:**
- Modify: `src/FairBank.Web.Products/Components/LoanCalculatorPanel.razor` — submit to API
- Modify: `src/FairBank.Web.Products/Components/MortgageCalculatorPanel.razor` — submit to API
- Modify: `src/FairBank.Web.Products/Components/InsurancePanel.razor` — add modals + submit to API

**Context:** Currently modals show static "Žádost přijata" text. Change to actually call the API. Also add `@inject IFairBankApi Api` and `@inject AuthStateService Auth`. For Banker/Admin roles, hide submit buttons. Insurance panel buttons have no handlers — add modals per sub-tab.

**Key pattern for each modal:**
```razor
@inject IFairBankApi Api
@inject AuthStateService Auth

<!-- Only show submit button for Client role -->
@if (!Auth.IsStaff)
{
    <VbButton OnClick="ShowModal">Požádat o úvěr</VbButton>
}

@if (_showModal)
{
    <div class="modal-overlay" @onclick="HideModal">
        <div class="modal-card" @onclick:stopPropagation>
            @if (_submitSuccess)
            {
                <h3>Žádost odeslána!</h3>
                <p>Vaše žádost čeká na schválení bankéřem.</p>
                <VbButton OnClick="HideModal">Zavřít</VbButton>
            }
            else if (_isSubmitting)
            {
                <h3>Odesílání...</h3>
            }
            else
            {
                <h3>Potvrdit žádost</h3>
                <p>Shrnutí parametrů...</p>
                <VbButton OnClick="SubmitApplication">Odeslat žádost</VbButton>
                <VbButton OnClick="HideModal">Zrušit</VbButton>
            }
        </div>
    </div>
}

@code {
    private bool _showModal, _isSubmitting, _submitSuccess;
    private string _errorMessage = "";

    private void ShowModal() { _showModal = true; _submitSuccess = false; _errorMessage = ""; }
    private void HideModal() { _showModal = false; _isSubmitting = false; }

    private async Task SubmitApplication()
    {
        _isSubmitting = true;
        try
        {
            var parameters = System.Text.Json.JsonSerializer.Serialize(new { /* product params */ });
            await Api.SubmitProductApplicationAsync(Auth.CurrentUser!.Id, "ProductType", parameters, monthlyPayment);
            _submitSuccess = true;
        }
        catch (Exception ex)
        {
            _errorMessage = "Žádost se nepodařilo odeslat.";
        }
        finally { _isSubmitting = false; }
    }
}
```

Implement this pattern for all 3 panels: LoanCalculatorPanel (PersonalLoan), MortgageCalculatorPanel (Mortgage), InsurancePanel (4 sub-tabs: TravelInsurance, PropertyInsurance, LifeInsurance, PaymentProtection).

**Step 1: Commit**

```bash
git add src/FairBank.Web.Products/Components/
git commit -m "feat(products): update calculator modals to submit applications via API"
```

---

### Task 8: Create "Moje produkty" Tab Component

**Files:**
- Create: `src/FairBank.Web.Products/Components/MyProductsPanel.razor`
- Modify: `src/FairBank.Web.Products/Pages/Products.razor` — add 4th tab

**Component shows:**
- List of user's product applications fetched from API
- Status badges (color-coded): Čeká (yellow), Schváleno/Aktivní (green), Zamítnuto (red), Zrušeno (gray)
- Product type label (Osobní úvěr, Hypotéka, Cestovní pojištění, etc.)
- Monthly payment, date submitted
- Cancel button for Pending applications
- Admin/Banker note if present

**Products.razor tab addition:**
```razor
<button class="product-tab @(_activeTab == "my" ? "active" : "")"
        @onclick='() => _activeTab = "my"'>
    <span class="tab-icon">📋</span> Moje produkty
</button>

<!-- In switch: -->
case "my":
    <MyProductsPanel />
    break;
```

**Only show "Moje produkty" tab for Client role** (not for Banker/Admin who use /sprava).

**Step 1: Commit**

```bash
git add src/FairBank.Web.Products/
git commit -m "feat(products): add 'Moje produkty' tab with application list and status badges"
```

---

### Task 9: Create Banker Management Page (`/sprava`)

**Files:**
- Create: `src/FairBank.Web.Products/Pages/Management.razor`
- Modify: `src/FairBank.Web/App.razor` — assembly already registered

**Page at `/sprava` shows:**
- Table of pending product applications
- Each row: applicant name (fetch from Identity), product type, amount, date
- Click row → detail with all JSON parameters parsed
- Approve/Reject buttons with optional note textarea
- Success/error feedback
- Only accessible by Banker and Admin roles (redirect others)

**Pattern:**
```razor
@page "/sprava"
@inject IFairBankApi Api
@inject AuthStateService Auth
@inject NavigationManager Nav

<PageHeader Title="SPRÁVA ŽÁDOSTÍ" />

@if (!Auth.IsStaff)
{
    // Redirect non-staff to overview
}

<!-- Pending applications table -->
<!-- Detail modal with approve/reject -->
```

**Step 1: Commit**

```bash
git add src/FairBank.Web.Products/Pages/Management.razor
git commit -m "feat(products): add Banker management page at /sprava for application review"
```

---

### Task 10: Add CSS Styles for New Components

**Files:**
- Modify: `src/FairBank.Web.Shared/wwwroot/css/vabank-theme.css` — add styles for status badges, application list, management table

**New CSS classes:**
- `.status-badge` with variants: `.status-pending` (yellow), `.status-active` (green), `.status-rejected` (red), `.status-cancelled` (gray)
- `.application-list` — list of product application cards
- `.application-card` — individual application with product icon, details, status
- `.management-table` — table for Banker/Admin view
- `.detail-modal` — expanded modal for application details
- `.note-textarea` — textarea for admin notes

**Step 1: Commit**

```bash
git add src/FairBank.Web.Shared/wwwroot/css/vabank-theme.css
git commit -m "feat(products): add CSS styles for application list, status badges, management table"
```

---

### Task 11: Docker Build & Integration Test

**Step 1: Rebuild all Docker containers**

```bash
docker compose down
docker compose up --build -d
```

**Step 2: Wait for healthy services**

```bash
docker compose ps
# Verify products-api is healthy
```

**Step 3: Test API directly**

```bash
# Health check
curl http://localhost:8080/api/v1/products/applications/pending

# Submit application (as client)
curl -X POST http://localhost:8080/api/v1/products/applications \
  -H "Content-Type: application/json" \
  -d '{"userId":"<client-user-id>","productType":"PersonalLoan","parameters":"{\"amount\":200000,\"months\":60,\"interestRate\":5.9}","monthlyPayment":3857}'
```

**Step 4: Test via browser**
- Login as client@fairbank.cz → Produkty → Osobní úvěr → Požádat → Odeslat
- Check "Moje produkty" tab — should show pending application
- Login as banker@fairbank.cz → Správa → should see pending application
- Approve it → check client's tab shows "Aktivní"

**Step 5: Commit any fixes**

```bash
git add -A
git commit -m "fix(products): integration test fixes"
```

---

### Task 12: Unit Tests for Domain & Application

**Files:**
- Create: `tests/FairBank.Products.Tests/FairBank.Products.Tests.csproj`
- Create: `tests/FairBank.Products.Tests/Domain/ProductApplicationTests.cs`
- Create: `tests/FairBank.Products.Tests/Application/SubmitApplicationCommandHandlerTests.cs`

**Domain tests:** Test Create(), Approve(), Reject(), Cancel() with valid and invalid state transitions.

**Application tests:** Test SubmitApplicationCommandHandler with mocked repository and unit of work.

**Step 1: Commit**

```bash
git add tests/FairBank.Products.Tests/
dotnet sln FairBank.slnx add tests/FairBank.Products.Tests/FairBank.Products.Tests.csproj
git add FairBank.slnx
git commit -m "test(products): add unit tests for domain aggregate and command handlers"
```
