# Loans & Insurance Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a "Produkty" section to FairBank with personal loan, mortgage, and insurance calculators — all frontend-only Blazor WASM, same pattern as `FairBank.Web.Exchange`.

**Architecture:** Single Razor Class Library `FairBank.Web.Products` with one page `/produkty`, three main tabs (Loan, Mortgage, Insurance), and calculator services in pure C#. Insurance tab has four sub-tabs. No backend service needed.

**Tech Stack:** Blazor WASM (.NET 10), xunit + FluentAssertions for tests, existing shared components (ContentCard, PageHeader, VbButton).

---

### Task 1: Scaffold project and wire into solution

**Files:**
- Create: `src/FairBank.Web.Products/FairBank.Web.Products.csproj`
- Create: `src/FairBank.Web.Products/_Imports.razor`
- Create: `src/FairBank.Web.Products/Pages/Products.razor` (placeholder)
- Modify: `FairBank.slnx` — add project under `/src/Web/`
- Modify: `src/FairBank.Web/FairBank.Web.csproj` — add ProjectReference
- Modify: `src/FairBank.Web/App.razor` — add assembly
- Modify: `src/FairBank.Web/Dockerfile` — add COPY lines
- Modify: `src/FairBank.Web/Layout/SideNav.razor` — add nav item
- Modify: `src/FairBank.Web/Layout/BottomNav.razor` — add nav item

**Step 1: Create the project file**

```xml
<!-- src/FairBank.Web.Products/FairBank.Web.Products.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <ItemGroup>
    <SupportedPlatform Include="browser" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../FairBank.Web.Shared/FairBank.Web.Shared.csproj" />
  </ItemGroup>
</Project>
```

**Step 2: Create _Imports.razor**

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Routing
@using FairBank.Web.Shared.Components
@using FairBank.Web.Shared.Models
@using FairBank.Web.Shared.Services
```

**Step 3: Create placeholder Products.razor**

```razor
@page "/produkty"
@namespace FairBank.Web.Products.Pages

<PageHeader Title="PRODUKTY" />

<div class="page-content">
    <ContentCard Title="Produkty">
        <ChildContent>
            <p>Brzy zde najdete naše produkty.</p>
        </ChildContent>
    </ContentCard>
</div>
```

**Step 4: Wire into solution**

Add to `FairBank.slnx` under `/src/Web/`:
```xml
<Project Path="src/FairBank.Web.Products/FairBank.Web.Products.csproj" />
```

Add to `src/FairBank.Web/FairBank.Web.csproj`:
```xml
<ProjectReference Include="../FairBank.Web.Products/FairBank.Web.Products.csproj" />
```

Add to `src/FairBank.Web/App.razor` AdditionalAssemblies:
```csharp
typeof(FairBank.Web.Products.Pages.Products).Assembly
```

Add to `src/FairBank.Web/Dockerfile` (both COPY csproj and COPY src blocks):
```dockerfile
COPY src/FairBank.Web.Products/FairBank.Web.Products.csproj    src/FairBank.Web.Products/
# ...
COPY src/FairBank.Web.Products/    src/FairBank.Web.Products/
```

Add nav items to `SideNav.razor` and `BottomNav.razor` (after Kurzy, before Zprávy):
```razor
<!-- SideNav -->
<NavLink class="side-nav-item" href="produkty">
    <span class="side-nav-icon">🏦</span>
    <span class="side-nav-label">Produkty</span>
</NavLink>

<!-- BottomNav -->
<NavLink class="nav-item" href="produkty">
    <span class="nav-icon">🏦</span>
    <span class="nav-label">Produkty</span>
</NavLink>
```

**Step 5: Verify build**

Run: `dotnet build src/FairBank.Web/FairBank.Web.csproj`
Expected: Build succeeded

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: scaffold FairBank.Web.Products module with nav integration"
```

---

### Task 2: Create LoanCalculator service with tests

**Files:**
- Create: `src/FairBank.Web.Products/Services/LoanCalculator.cs`
- Create: `tests/FairBank.Web.Products.Tests/FairBank.Web.Products.Tests.csproj`
- Create: `tests/FairBank.Web.Products.Tests/LoanCalculatorTests.cs`

**Step 1: Create test project**

```xml
<!-- tests/FairBank.Web.Products.Tests/FairBank.Web.Products.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/FairBank.Web.Products/FairBank.Web.Products.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentAssertions" />
  </ItemGroup>
</Project>
```

Add to `FairBank.slnx` under `/tests/`:
```xml
<Project Path="tests/FairBank.Web.Products.Tests/FairBank.Web.Products.Tests.csproj" />
```

**Step 2: Write failing tests**

```csharp
// tests/FairBank.Web.Products.Tests/LoanCalculatorTests.cs
using FairBank.Web.Products.Services;

namespace FairBank.Web.Products.Tests;

public class LoanCalculatorTests
{
    // ── Rate tier tests ──

    [Theory]
    [InlineData(50_000, 8.9)]
    [InlineData(100_000, 8.9)]
    [InlineData(100_001, 5.9)]
    [InlineData(300_000, 5.9)]
    [InlineData(500_000, 5.9)]
    [InlineData(500_001, 5.4)]
    [InlineData(1_000_000, 5.4)]
    [InlineData(1_000_001, 4.9)]
    [InlineData(2_000_000, 4.9)]
    public void GetInterestRate_ReturnsCorrectTier(decimal amount, decimal expectedRate)
    {
        LoanCalculator.GetInterestRate(amount).Should().Be(expectedRate);
    }

    // ── Annuity tests ──

    [Fact]
    public void CalculateMonthlyPayment_200k_60months_5point9percent()
    {
        // 200,000 CZK, 60 months, 5.9% p.a.
        var payment = LoanCalculator.CalculateMonthlyPayment(200_000m, 60, 5.9m);
        // Expected annuity ~3,856 CZK (standard annuity formula)
        payment.Should().BeApproximately(3856m, 5m);
    }

    [Fact]
    public void CalculateMonthlyPayment_1M_96months_5point4percent()
    {
        var payment = LoanCalculator.CalculateMonthlyPayment(1_000_000m, 96, 5.4m);
        // Expected ~13,267 CZK
        payment.Should().BeApproximately(13267m, 10m);
    }

    [Fact]
    public void CalculateMonthlyPayment_ZeroAmount_ReturnsZero()
    {
        var payment = LoanCalculator.CalculateMonthlyPayment(0m, 60, 5.9m);
        payment.Should().Be(0m);
    }

    // ── RPSN test ──

    [Fact]
    public void GetRpsn_AddsPointTwo()
    {
        LoanCalculator.GetRpsn(5.9m).Should().Be(6.1m);
    }

    // ── Total cost test ──

    [Fact]
    public void GetTotalCost_MultipliesPaymentByMonths()
    {
        var total = LoanCalculator.GetTotalCost(3856m, 60);
        total.Should().Be(231_360m);
    }

    // ── Full calculation test ──

    [Fact]
    public void Calculate_ReturnsAllFields()
    {
        var result = LoanCalculator.Calculate(200_000m, 60);
        result.InterestRate.Should().Be(5.9m);
        result.Rpsn.Should().Be(6.1m);
        result.MonthlyPayment.Should().BeGreaterThan(0);
        result.TotalCost.Should().BeGreaterThan(200_000m);
    }
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test tests/FairBank.Web.Products.Tests/ --filter "LoanCalculator" -v m`
Expected: FAIL — LoanCalculator class does not exist

**Step 4: Write LoanCalculator implementation**

```csharp
// src/FairBank.Web.Products/Services/LoanCalculator.cs
namespace FairBank.Web.Products.Services;

public static class LoanCalculator
{
    public record LoanResult(
        decimal MonthlyPayment,
        decimal InterestRate,
        decimal Rpsn,
        decimal TotalCost);

    /// <summary>Interest rate tier based on loan amount.</summary>
    public static decimal GetInterestRate(decimal amount) => amount switch
    {
        <= 100_000m => 8.9m,
        <= 500_000m => 5.9m,
        <= 1_000_000m => 5.4m,
        _ => 4.9m
    };

    /// <summary>RPSN = interest rate + 0.2%.</summary>
    public static decimal GetRpsn(decimal interestRate) => interestRate + 0.2m;

    /// <summary>Standard annuity formula.</summary>
    public static decimal CalculateMonthlyPayment(decimal principal, int months, decimal annualRate)
    {
        if (principal <= 0 || months <= 0)
            return 0m;

        var monthlyRate = annualRate / 100m / 12m;

        if (monthlyRate == 0)
            return principal / months;

        var factor = (decimal)Math.Pow((double)(1 + monthlyRate), months);
        return Math.Round(principal * monthlyRate * factor / (factor - 1), 0);
    }

    /// <summary>Total cost = monthly payment * months.</summary>
    public static decimal GetTotalCost(decimal monthlyPayment, int months)
        => monthlyPayment * months;

    /// <summary>Full calculation from amount and term.</summary>
    public static LoanResult Calculate(decimal amount, int months)
    {
        var rate = GetInterestRate(amount);
        var payment = CalculateMonthlyPayment(amount, months, rate);
        return new LoanResult(payment, rate, GetRpsn(rate), GetTotalCost(payment, months));
    }
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/FairBank.Web.Products.Tests/ --filter "LoanCalculator" -v m`
Expected: All PASS

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add LoanCalculator service with annuity math and rate tiers"
```

---

### Task 3: Create MortgageCalculator service with tests

**Files:**
- Create: `src/FairBank.Web.Products/Services/MortgageCalculator.cs`
- Create: `tests/FairBank.Web.Products.Tests/MortgageCalculatorTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/FairBank.Web.Products.Tests/MortgageCalculatorTests.cs
using FairBank.Web.Products.Services;

namespace FairBank.Web.Products.Tests;

public class MortgageCalculatorTests
{
    // ── LTV calculation ──

    [Fact]
    public void GetLtv_3200kLoan_4000kProperty_Returns80()
    {
        MortgageCalculator.GetLtv(3_200_000m, 4_000_000m).Should().Be(80m);
    }

    [Fact]
    public void GetLtv_ZeroProperty_ReturnsZero()
    {
        MortgageCalculator.GetLtv(1_000_000m, 0m).Should().Be(0m);
    }

    // ── Rate matrix tests ──

    [Theory]
    [InlineData(1, 50, 5.29)]   // 1yr fix, LTV ≤60%
    [InlineData(1, 75, 5.59)]   // 1yr fix, LTV 60-80%
    [InlineData(3, 50, 4.89)]   // 3yr fix, LTV ≤60%
    [InlineData(3, 75, 5.19)]   // 3yr fix, LTV 60-80%
    [InlineData(5, 50, 4.49)]   // 5yr fix, LTV ≤60%
    [InlineData(5, 75, 4.79)]   // 5yr fix, LTV 60-80%
    [InlineData(10, 50, 4.99)]  // 10yr fix, LTV ≤60%
    [InlineData(10, 75, 5.29)]  // 10yr fix, LTV 60-80%
    public void GetInterestRate_ReturnsCorrectRate(int fixationYears, decimal ltv, decimal expectedRate)
    {
        MortgageCalculator.GetInterestRate(fixationYears, ltv).Should().Be(expectedRate);
    }

    [Fact]
    public void GetInterestRate_UnknownFixation_Returns5point59()
    {
        // Default fallback for unknown fixation
        MortgageCalculator.GetInterestRate(7, 50m).Should().Be(5.59m);
    }

    // ── Full calculation ──

    [Fact]
    public void Calculate_StandardMortgage()
    {
        // 4M property, 3.2M loan (80% LTV), 25 years, 5yr fixation
        var result = MortgageCalculator.Calculate(4_000_000m, 3_200_000m, 25, 5);
        result.InterestRate.Should().Be(4.79m);
        result.Ltv.Should().Be(80m);
        result.OwnResources.Should().Be(800_000m);
        result.MonthlyPayment.Should().BeGreaterThan(0);
        result.TotalCost.Should().BeGreaterThan(3_200_000m);
    }

    [Fact]
    public void Calculate_LowLtv()
    {
        // 5M property, 2M loan (40% LTV), 20 years, 5yr fixation
        var result = MortgageCalculator.Calculate(5_000_000m, 2_000_000m, 20, 5);
        result.InterestRate.Should().Be(4.49m);
        result.Ltv.Should().Be(40m);
        result.OwnResources.Should().Be(3_000_000m);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/FairBank.Web.Products.Tests/ --filter "MortgageCalculator" -v m`
Expected: FAIL

**Step 3: Write MortgageCalculator implementation**

```csharp
// src/FairBank.Web.Products/Services/MortgageCalculator.cs
namespace FairBank.Web.Products.Services;

public static class MortgageCalculator
{
    public record MortgageResult(
        decimal MonthlyPayment,
        decimal InterestRate,
        decimal Ltv,
        decimal OwnResources,
        decimal TotalCost);

    public static readonly int[] FixationOptions = [1, 3, 5, 10];

    /// <summary>Loan-to-Value ratio as percentage.</summary>
    public static decimal GetLtv(decimal loanAmount, decimal propertyPrice)
        => propertyPrice > 0 ? Math.Round(loanAmount / propertyPrice * 100, 1) : 0m;

    /// <summary>Interest rate from fixation period and LTV band.</summary>
    public static decimal GetInterestRate(int fixationYears, decimal ltv)
    {
        var isLowLtv = ltv <= 60m;
        return fixationYears switch
        {
            1  => isLowLtv ? 5.29m : 5.59m,
            3  => isLowLtv ? 4.89m : 5.19m,
            5  => isLowLtv ? 4.49m : 4.79m,
            10 => isLowLtv ? 4.99m : 5.29m,
            _  => isLowLtv ? 5.29m : 5.59m  // fallback = 1yr rates
        };
    }

    /// <summary>Standard annuity formula (same math as LoanCalculator).</summary>
    public static decimal CalculateMonthlyPayment(decimal principal, int years, decimal annualRate)
    {
        if (principal <= 0 || years <= 0)
            return 0m;

        var months = years * 12;
        var monthlyRate = annualRate / 100m / 12m;

        if (monthlyRate == 0)
            return principal / months;

        var factor = (decimal)Math.Pow((double)(1 + monthlyRate), months);
        return Math.Round(principal * monthlyRate * factor / (factor - 1), 0);
    }

    /// <summary>Full mortgage calculation.</summary>
    public static MortgageResult Calculate(decimal propertyPrice, decimal loanAmount, int years, int fixationYears)
    {
        var ltv = GetLtv(loanAmount, propertyPrice);
        var rate = GetInterestRate(fixationYears, ltv);
        var payment = CalculateMonthlyPayment(loanAmount, years, rate);
        var totalCost = payment * years * 12;
        var ownResources = propertyPrice - loanAmount;

        return new MortgageResult(payment, rate, ltv, ownResources, totalCost);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/FairBank.Web.Products.Tests/ --filter "MortgageCalculator" -v m`
Expected: All PASS

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add MortgageCalculator with LTV tiers, fixation rates"
```

---

### Task 4: Create InsuranceCalculator service with tests

**Files:**
- Create: `src/FairBank.Web.Products/Services/InsuranceCalculator.cs`
- Create: `tests/FairBank.Web.Products.Tests/InsuranceCalculatorTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/FairBank.Web.Products.Tests/InsuranceCalculatorTests.cs
using FairBank.Web.Products.Services;

namespace FairBank.Web.Products.Tests;

public class InsuranceCalculatorTests
{
    // ── Travel insurance ──

    [Theory]
    [InlineData("europe", "standard", 1, 1, 35)]
    [InlineData("europe", "plus", 1, 1, 65)]
    [InlineData("world", "standard", 1, 1, 75)]
    [InlineData("world", "plus", 1, 1, 120)]
    [InlineData("europe", "standard", 7, 2, 490)]  // 35 * 7 * 2
    [InlineData("world", "plus", 14, 3, 5040)]     // 120 * 14 * 3
    public void CalculateTravel_ReturnsCorrectPrice(
        string destination, string variant, int days, int persons, decimal expected)
    {
        InsuranceCalculator.CalculateTravel(destination, variant, days, persons)
            .Should().Be(expected);
    }

    // ── Property insurance ──

    [Theory]
    [InlineData("apartment", 2_000_000, false, 1600)]   // 0.08% of 2M
    [InlineData("house", 5_000_000, false, 6000)]        // 0.12% of 5M
    [InlineData("apartment", 2_000_000, true, 2240)]     // 1600 * 1.4
    [InlineData("house", 5_000_000, true, 8400)]          // 6000 * 1.4
    public void CalculateProperty_ReturnsCorrectAnnualPremium(
        string type, decimal value, bool includeContents, decimal expected)
    {
        InsuranceCalculator.CalculatePropertyAnnual(type, value, includeContents)
            .Should().Be(expected);
    }

    // ── Life insurance ──

    [Fact]
    public void CalculateLife_Age30_1M_Risk_ReturnsReasonableAmount()
    {
        var monthly = InsuranceCalculator.CalculateLifeMonthly(30, 1_000_000m, "risk");
        monthly.Should().BeInRange(200m, 500m);
    }

    [Fact]
    public void CalculateLife_Age50_HigherThanAge30()
    {
        var young = InsuranceCalculator.CalculateLifeMonthly(30, 1_000_000m, "risk");
        var older = InsuranceCalculator.CalculateLifeMonthly(50, 1_000_000m, "risk");
        older.Should().BeGreaterThan(young);
    }

    [Fact]
    public void CalculateLife_Investment_HigherThanRisk()
    {
        var risk = InsuranceCalculator.CalculateLifeMonthly(30, 1_000_000m, "risk");
        var invest = InsuranceCalculator.CalculateLifeMonthly(30, 1_000_000m, "investment");
        invest.Should().BeGreaterThan(risk);
    }

    // ── Payment protection ──

    [Theory]
    [InlineData(10000, "standard", 550)]   // 5.5%
    [InlineData(10000, "plus", 850)]       // 8.5%
    [InlineData(5000, "standard", 275)]
    public void CalculatePaymentProtection_ReturnsCorrectAmount(
        decimal monthlyPayment, string variant, decimal expected)
    {
        InsuranceCalculator.CalculatePaymentProtection(monthlyPayment, variant)
            .Should().Be(expected);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/FairBank.Web.Products.Tests/ --filter "InsuranceCalculator" -v m`
Expected: FAIL

**Step 3: Write InsuranceCalculator implementation**

```csharp
// src/FairBank.Web.Products/Services/InsuranceCalculator.cs
namespace FairBank.Web.Products.Services;

public static class InsuranceCalculator
{
    // ── Travel Insurance ──

    private static readonly Dictionary<(string dest, string variant), decimal> TravelRates = new()
    {
        [("europe", "standard")] = 35m,
        [("europe", "plus")] = 65m,
        [("world", "standard")] = 75m,
        [("world", "plus")] = 120m,
    };

    /// <summary>Total travel insurance price.</summary>
    public static decimal CalculateTravel(string destination, string variant, int days, int persons)
    {
        var key = (destination.ToLowerInvariant(), variant.ToLowerInvariant());
        var rate = TravelRates.GetValueOrDefault(key, 35m);
        return rate * days * persons;
    }

    // ── Property Insurance ──

    /// <summary>Annual property insurance premium.</summary>
    public static decimal CalculatePropertyAnnual(string propertyType, decimal propertyValue, bool includeContents)
    {
        var baseRate = propertyType.ToLowerInvariant() == "house" ? 0.0012m : 0.0008m;
        var annual = Math.Round(propertyValue * baseRate, 0);
        if (includeContents)
            annual = Math.Round(annual * 1.4m, 0);
        return annual;
    }

    /// <summary>Monthly property insurance premium.</summary>
    public static decimal CalculatePropertyMonthly(string propertyType, decimal propertyValue, bool includeContents)
        => Math.Round(CalculatePropertyAnnual(propertyType, propertyValue, includeContents) / 12m, 0);

    // ── Life Insurance ──

    /// <summary>Age coefficient for life insurance pricing.</summary>
    public static decimal GetAgeCoefficient(int age) => age switch
    {
        <= 25 => 0.8m,
        <= 35 => 1.0m,
        <= 45 => 1.5m,
        <= 55 => 2.2m,
        _ => 3.0m
    };

    /// <summary>Monthly life insurance premium.</summary>
    public static decimal CalculateLifeMonthly(int age, decimal coverageAmount, string variant)
    {
        var baseRate = variant.ToLowerInvariant() == "investment" ? 0.0004m : 0.0003m;
        var ageCoef = GetAgeCoefficient(age);
        return Math.Round(coverageAmount * baseRate * ageCoef / 12m, 0);
    }

    // ── Payment Protection Insurance ──

    /// <summary>Monthly payment protection premium.</summary>
    public static decimal CalculatePaymentProtection(decimal monthlyPayment, string variant)
    {
        var rate = variant.ToLowerInvariant() == "plus" ? 0.085m : 0.055m;
        return Math.Round(monthlyPayment * rate, 0);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/FairBank.Web.Products.Tests/ --filter "InsuranceCalculator" -v m`
Expected: All PASS

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add InsuranceCalculator — travel, property, life, payment protection"
```

---

### Task 5: Build Products.razor main page with tab navigation

**Files:**
- Modify: `src/FairBank.Web.Products/Pages/Products.razor` — full page with tab switching

**Step 1: Write the main page with tabs**

Replace placeholder in `Products.razor`:

```razor
@page "/produkty"
@namespace FairBank.Web.Products.Pages
@using FairBank.Web.Products.Services

<PageHeader Title="PRODUKTY" />

<div class="page-content">

    @* ── Tab Navigation ── *@
    <div class="product-tabs">
        <button class="product-tab @(_activeTab == "loan" ? "active" : "")"
                @onclick='() => _activeTab = "loan"'>
            <span class="tab-icon">💰</span> Osobní úvěr
        </button>
        <button class="product-tab @(_activeTab == "mortgage" ? "active" : "")"
                @onclick='() => _activeTab = "mortgage"'>
            <span class="tab-icon">🏠</span> Hypotéka
        </button>
        <button class="product-tab @(_activeTab == "insurance" ? "active" : "")"
                @onclick='() => _activeTab = "insurance"'>
            <span class="tab-icon">🛡️</span> Pojištění
        </button>
    </div>

    @* ── Tab Content ── *@
    @switch (_activeTab)
    {
        case "loan":
            <LoanCalculatorPanel />
            break;
        case "mortgage":
            <MortgageCalculatorPanel />
            break;
        case "insurance":
            <InsurancePanel />
            break;
    }

</div>

@code {
    private string _activeTab = "loan";
}
```

Note: The component references (LoanCalculatorPanel, MortgageCalculatorPanel, InsurancePanel) will be created in the following tasks. For now, create stubs so the page compiles.

**Step 2: Create component stubs**

Create `src/FairBank.Web.Products/Components/LoanCalculatorPanel.razor`:
```razor
@namespace FairBank.Web.Products.Pages
<ContentCard Title="Osobní úvěr"><ChildContent><p>Loading...</p></ChildContent></ContentCard>
```

Create `src/FairBank.Web.Products/Components/MortgageCalculatorPanel.razor`:
```razor
@namespace FairBank.Web.Products.Pages
<ContentCard Title="Hypotéka"><ChildContent><p>Loading...</p></ChildContent></ContentCard>
```

Create `src/FairBank.Web.Products/Components/InsurancePanel.razor`:
```razor
@namespace FairBank.Web.Products.Pages
<ContentCard Title="Pojištění"><ChildContent><p>Loading...</p></ChildContent></ContentCard>
```

**Step 3: Verify build**

Run: `dotnet build src/FairBank.Web/FairBank.Web.csproj`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add Products page with tab navigation and component stubs"
```

---

### Task 6: Implement LoanCalculatorPanel component

**Files:**
- Modify: `src/FairBank.Web.Products/Components/LoanCalculatorPanel.razor`

**Step 1: Write the full loan calculator UI**

```razor
@namespace FairBank.Web.Products.Pages
@using FairBank.Web.Products.Services

<ContentCard Title="Kalkulačka osobního úvěru">
    <ChildContent>
        <div class="calculator-form">
            <div class="form-group">
                <label class="form-label">Částka úvěru</label>
                <input class="form-input" type="range" min="10000" max="2000000" step="10000"
                       @bind="_amount" @bind:event="oninput" />
                <div class="range-value">@_amount.ToString("N0") Kč</div>
            </div>

            <div class="form-group">
                <label class="form-label">Doba splácení (měsíce)</label>
                <input class="form-input" type="range" min="6" max="96" step="6"
                       @bind="_months" @bind:event="oninput" />
                <div class="range-value">@_months měsíců (@(_months / 12) let @(_months % 12 > 0 ? $"a {_months % 12} měs." : ""))</div>
            </div>
        </div>

        <div class="calc-results">
            <div class="calc-result-primary">
                <span class="result-label">Měsíční splátka</span>
                <span class="result-value">@_result.MonthlyPayment.ToString("N0") Kč</span>
            </div>
            <div class="calc-results-grid">
                <div class="calc-result-item">
                    <span class="result-label">Úroková sazba</span>
                    <span class="result-value-sm">@_result.InterestRate.ToString("F1") % p.a.</span>
                </div>
                <div class="calc-result-item">
                    <span class="result-label">RPSN</span>
                    <span class="result-value-sm">@_result.Rpsn.ToString("F1") %</span>
                </div>
                <div class="calc-result-item">
                    <span class="result-label">Celkem zaplatíte</span>
                    <span class="result-value-sm">@_result.TotalCost.ToString("N0") Kč</span>
                </div>
            </div>
        </div>

        <VbButton OnClick="ShowModal">Požádat o úvěr</VbButton>

        <div class="representative-example">
            <strong>Reprezentativní příklad:</strong>
            Výše úvěru @_amount.ToString("N0") Kč, doba splácení @_months měsíců,
            úroková sazba @_result.InterestRate.ToString("F1") % p.a. (fixní),
            RPSN @_result.Rpsn.ToString("F1") %, měsíční splátka @_result.MonthlyPayment.ToString("N0") Kč,
            celková částka splatná @_result.TotalCost.ToString("N0") Kč.
        </div>

        @if (_showModal)
        {
            <div class="modal-overlay" @onclick="HideModal">
                <div class="modal-card" @onclick:stopPropagation>
                    <h3>Děkujeme za váš zájem!</h3>
                    <p>Vaše žádost o úvěr ve výši @_amount.ToString("N0") Kč byla přijata.
                       Náš bankéř vás bude kontaktovat do 24 hodin.</p>
                    <VbButton OnClick="HideModal">Zavřít</VbButton>
                </div>
            </div>
        }
    </ChildContent>
</ContentCard>

@code {
    private decimal _amount = 200_000m;
    private int _months = 60;
    private bool _showModal;

    private LoanCalculator.LoanResult _result => LoanCalculator.Calculate(_amount, _months);

    private void ShowModal() => _showModal = true;
    private void HideModal() => _showModal = false;
}
```

**Step 2: Verify build**

Run: `dotnet build src/FairBank.Web/FairBank.Web.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: implement LoanCalculatorPanel with slider inputs and real-time results"
```

---

### Task 7: Implement MortgageCalculatorPanel component

**Files:**
- Modify: `src/FairBank.Web.Products/Components/MortgageCalculatorPanel.razor`

**Step 1: Write the full mortgage calculator UI**

```razor
@namespace FairBank.Web.Products.Pages
@using FairBank.Web.Products.Services

<ContentCard Title="Hypoteční kalkulačka">
    <ChildContent>
        <div class="calculator-form">
            <div class="form-group">
                <label class="form-label">Cena nemovitosti</label>
                <input class="form-input" type="range" min="500000" max="20000000" step="100000"
                       @bind="_propertyPrice" @bind:event="oninput" />
                <div class="range-value">@_propertyPrice.ToString("N0") Kč</div>
            </div>

            <div class="form-group">
                <label class="form-label">Výše úvěru (max @_maxLoan.ToString("N0") Kč = 80% LTV)</label>
                <input class="form-input" type="range" min="300000" max="@_maxLoan" step="100000"
                       value="@_loanAmount" @oninput="OnLoanInput" />
                <div class="range-value">@_loanAmount.ToString("N0") Kč</div>
            </div>

            <div class="form-group">
                <label class="form-label">Doba splácení</label>
                <input class="form-input" type="range" min="5" max="30" step="1"
                       @bind="_years" @bind:event="oninput" />
                <div class="range-value">@_years let</div>
            </div>

            <div class="form-group">
                <label class="form-label">Fixace úrokové sazby</label>
                <div class="fixation-options">
                    @foreach (var fix in MortgageCalculator.FixationOptions)
                    {
                        <button class="fixation-btn @(_fixation == fix ? "active" : "")"
                                @onclick="() => _fixation = fix">
                            @fix @(fix == 1 ? "rok" : fix < 5 ? "roky" : "let")
                        </button>
                    }
                </div>
            </div>
        </div>

        <div class="calc-results">
            <div class="calc-result-primary">
                <span class="result-label">Měsíční splátka</span>
                <span class="result-value">@_result.MonthlyPayment.ToString("N0") Kč</span>
            </div>
            <div class="calc-results-grid">
                <div class="calc-result-item">
                    <span class="result-label">Úroková sazba</span>
                    <span class="result-value-sm">@_result.InterestRate.ToString("F2") % p.a.</span>
                </div>
                <div class="calc-result-item">
                    <span class="result-label">LTV</span>
                    <span class="result-value-sm">@_result.Ltv.ToString("F0") %</span>
                </div>
                <div class="calc-result-item">
                    <span class="result-label">Vlastní zdroje</span>
                    <span class="result-value-sm">@_result.OwnResources.ToString("N0") Kč</span>
                </div>
                <div class="calc-result-item">
                    <span class="result-label">Celkem zaplatíte</span>
                    <span class="result-value-sm">@_result.TotalCost.ToString("N0") Kč</span>
                </div>
            </div>
        </div>

        <VbButton OnClick="ShowModal">Požádat o hypotéku</VbButton>

        <div class="representative-example">
            <strong>Reprezentativní příklad:</strong>
            Cena nemovitosti @_propertyPrice.ToString("N0") Kč,
            výše úvěru @_loanAmount.ToString("N0") Kč (LTV @_result.Ltv.ToString("F0") %),
            doba splácení @_years let, fixace @_fixation @(_fixation == 1 ? "rok" : _fixation < 5 ? "roky" : "let"),
            úroková sazba @_result.InterestRate.ToString("F2") % p.a.,
            měsíční splátka @_result.MonthlyPayment.ToString("N0") Kč,
            celková částka splatná @_result.TotalCost.ToString("N0") Kč.
        </div>

        @if (_showModal)
        {
            <div class="modal-overlay" @onclick="HideModal">
                <div class="modal-card" @onclick:stopPropagation>
                    <h3>Děkujeme za váš zájem!</h3>
                    <p>Vaše žádost o hypotéku ve výši @_loanAmount.ToString("N0") Kč byla přijata.
                       Náš hypoteční specialista vás bude kontaktovat do 48 hodin.</p>
                    <VbButton OnClick="HideModal">Zavřít</VbButton>
                </div>
            </div>
        }
    </ChildContent>
</ContentCard>

@code {
    private decimal _propertyPrice = 4_000_000m;
    private decimal _loanAmount = 3_200_000m;
    private int _years = 25;
    private int _fixation = 5;
    private bool _showModal;

    private decimal _maxLoan => Math.Floor(_propertyPrice * 0.8m / 100_000m) * 100_000m;

    private MortgageCalculator.MortgageResult _result
        => MortgageCalculator.Calculate(_propertyPrice, Math.Min(_loanAmount, _maxLoan), _years, _fixation);

    private void OnLoanInput(ChangeEventArgs e)
    {
        if (decimal.TryParse(e.Value?.ToString(), out var val))
            _loanAmount = Math.Min(val, _maxLoan);
    }

    private void ShowModal() => _showModal = true;
    private void HideModal() => _showModal = false;
}
```

**Step 2: Verify build**

Run: `dotnet build src/FairBank.Web/FairBank.Web.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: implement MortgageCalculatorPanel with LTV, fixation, sliders"
```

---

### Task 8: Implement InsurancePanel component with 4 sub-tabs

**Files:**
- Modify: `src/FairBank.Web.Products/Components/InsurancePanel.razor`

**Step 1: Write the full insurance panel with sub-tabs**

```razor
@namespace FairBank.Web.Products.Pages
@using FairBank.Web.Products.Services

<div class="insurance-subtabs">
    <button class="insurance-subtab @(_subTab == "travel" ? "active" : "")"
            @onclick='() => _subTab = "travel"'>✈️ Cestovní</button>
    <button class="insurance-subtab @(_subTab == "property" ? "active" : "")"
            @onclick='() => _subTab = "property"'>🏠 Nemovitost</button>
    <button class="insurance-subtab @(_subTab == "life" ? "active" : "")"
            @onclick='() => _subTab = "life"'>❤️ Životní</button>
    <button class="insurance-subtab @(_subTab == "protection" ? "active" : "")"
            @onclick='() => _subTab = "protection"'>🔒 Ochrana splátek</button>
</div>

@switch (_subTab)
{
    case "travel":
        <ContentCard Title="Cestovní pojištění">
            <ChildContent>
                <div class="calculator-form">
                    <div class="form-group">
                        <label class="form-label">Destinace</label>
                        <div class="radio-group">
                            <label class="radio-option @(_travelDest == "europe" ? "active" : "")">
                                <input type="radio" name="dest" value="europe" @onchange='() => _travelDest = "europe"' checked="@(_travelDest == "europe")" /> 🇪🇺 Evropa
                            </label>
                            <label class="radio-option @(_travelDest == "world" ? "active" : "")">
                                <input type="radio" name="dest" value="world" @onchange='() => _travelDest = "world"' checked="@(_travelDest == "world")" /> 🌍 Svět
                            </label>
                        </div>
                    </div>
                    <div class="form-group">
                        <label class="form-label">Počet dní</label>
                        <input class="form-input" type="number" min="1" max="90" @bind="_travelDays" />
                    </div>
                    <div class="form-group">
                        <label class="form-label">Počet osob</label>
                        <input class="form-input" type="number" min="1" max="6" @bind="_travelPersons" />
                    </div>
                    <div class="form-group">
                        <label class="form-label">Varianta</label>
                        <div class="radio-group">
                            <label class="radio-option @(_travelVariant == "standard" ? "active" : "")">
                                <input type="radio" name="tvar" value="standard" @onchange='() => _travelVariant = "standard"' checked="@(_travelVariant == "standard")" /> Standard
                            </label>
                            <label class="radio-option @(_travelVariant == "plus" ? "active" : "")">
                                <input type="radio" name="tvar" value="plus" @onchange='() => _travelVariant = "plus"' checked="@(_travelVariant == "plus")" /> Plus
                            </label>
                        </div>
                    </div>
                </div>
                <div class="calc-results">
                    <div class="calc-result-primary">
                        <span class="result-label">Celková cena</span>
                        <span class="result-value">@_travelPrice.ToString("N0") Kč</span>
                    </div>
                </div>
                <div class="coverage-list">
                    <h4>Krytí zahrnuje:</h4>
                    <ul>
                        <li>Léčebné výlohy v zahraničí — @(_travelVariant == "plus" ? "100 000 000" : "5 000 000") Kč</li>
                        <li>Storno poplatky — @(_travelVariant == "plus" ? "50 000" : "25 000") Kč</li>
                        <li>Zavazadla — @(_travelVariant == "plus" ? "40 000" : "20 000") Kč</li>
                        <li>Odpovědnost za škodu — @(_travelVariant == "plus" ? "5 000 000" : "2 000 000") Kč</li>
                        @if (_travelVariant == "plus")
                        {
                            <li>Úrazové pojištění — 500 000 Kč</li>
                            <li>Právní pomoc v zahraničí</li>
                        }
                    </ul>
                </div>
                <VbButton>Sjednat online</VbButton>
            </ChildContent>
        </ContentCard>
        break;

    case "property":
        <ContentCard Title="Pojištění nemovitosti">
            <ChildContent>
                <div class="calculator-form">
                    <div class="form-group">
                        <label class="form-label">Typ nemovitosti</label>
                        <div class="radio-group">
                            <label class="radio-option @(_propType == "apartment" ? "active" : "")">
                                <input type="radio" name="ptype" value="apartment" @onchange='() => _propType = "apartment"' checked="@(_propType == "apartment")" /> 🏢 Byt
                            </label>
                            <label class="radio-option @(_propType == "house" ? "active" : "")">
                                <input type="radio" name="ptype" value="house" @onchange='() => _propType = "house"' checked="@(_propType == "house")" /> 🏡 Dům
                            </label>
                        </div>
                    </div>
                    <div class="form-group">
                        <label class="form-label">Hodnota nemovitosti</label>
                        <input class="form-input" type="range" min="500000" max="15000000" step="100000"
                               @bind="_propValue" @bind:event="oninput" />
                        <div class="range-value">@_propValue.ToString("N0") Kč</div>
                    </div>
                    <div class="form-group">
                        <label class="checkbox-label">
                            <input type="checkbox" @bind="_propContents" />
                            Včetně pojištění domácnosti (+40%)
                        </label>
                    </div>
                </div>
                <div class="calc-results">
                    <div class="calc-result-primary">
                        <span class="result-label">Roční pojistné</span>
                        <span class="result-value">@_propAnnual.ToString("N0") Kč</span>
                    </div>
                    <div class="calc-results-grid">
                        <div class="calc-result-item">
                            <span class="result-label">Měsíční pojistné</span>
                            <span class="result-value-sm">@_propMonthly.ToString("N0") Kč</span>
                        </div>
                    </div>
                </div>
                <div class="coverage-list">
                    <h4>Krytí zahrnuje:</h4>
                    <ul>
                        <li>Požár, výbuch, úder blesku</li>
                        <li>Povodeň a záplava</li>
                        <li>Vichřice, krupobití</li>
                        <li>Krádež a vandalismus</li>
                        <li>Škody vodou z instalací</li>
                        @if (_propContents)
                        {
                            <li>Vybavení a osobní věci</li>
                            <li>Elektronika a spotřebiče</li>
                        }
                    </ul>
                </div>
                <VbButton>Sjednat online</VbButton>
            </ChildContent>
        </ContentCard>
        break;

    case "life":
        <ContentCard Title="Životní pojištění">
            <ChildContent>
                <div class="calculator-form">
                    <div class="form-group">
                        <label class="form-label">Věk</label>
                        <input class="form-input" type="number" min="18" max="65" @bind="_lifeAge" />
                    </div>
                    <div class="form-group">
                        <label class="form-label">Pojistná částka</label>
                        <input class="form-input" type="range" min="200000" max="5000000" step="100000"
                               @bind="_lifeCoverage" @bind:event="oninput" />
                        <div class="range-value">@_lifeCoverage.ToString("N0") Kč</div>
                    </div>
                    <div class="form-group">
                        <label class="form-label">Varianta</label>
                        <div class="radio-group">
                            <label class="radio-option @(_lifeVariant == "risk" ? "active" : "")">
                                <input type="radio" name="lvar" value="risk" @onchange='() => _lifeVariant = "risk"' checked="@(_lifeVariant == "risk")" /> Rizikové
                            </label>
                            <label class="radio-option @(_lifeVariant == "investment" ? "active" : "")">
                                <input type="radio" name="lvar" value="investment" @onchange='() => _lifeVariant = "investment"' checked="@(_lifeVariant == "investment")" /> Investiční
                            </label>
                        </div>
                    </div>
                </div>
                <div class="calc-results">
                    <div class="calc-result-primary">
                        <span class="result-label">Měsíční pojistné</span>
                        <span class="result-value">@_lifeMonthly.ToString("N0") Kč</span>
                    </div>
                </div>
                <div class="coverage-list">
                    <h4>Krytí zahrnuje:</h4>
                    <ul>
                        <li>Úmrtí — výplata pojistné částky</li>
                        <li>Invalidita III. stupně — výplata pojistné částky</li>
                        <li>Invalidita II. stupně — 50% pojistné částky</li>
                        <li>Závažná onemocnění — 25% pojistné částky</li>
                        @if (_lifeVariant == "investment")
                        {
                            <li>Investiční složka — zhodnocení prostředků</li>
                            <li>Daňový odpočet až 24 000 Kč/rok</li>
                        }
                    </ul>
                </div>
                <VbButton>Sjednat online</VbButton>
            </ChildContent>
        </ContentCard>
        break;

    case "protection":
        <ContentCard Title="Pojištění schopnosti splácet">
            <ChildContent>
                <div class="calculator-form">
                    <div class="form-group">
                        <label class="form-label">Měsíční splátka úvěru/hypotéky</label>
                        <input class="form-input" type="number" min="500" max="100000" step="100"
                               @bind="_protPayment" />
                        <div class="range-value">@_protPayment.ToString("N0") Kč</div>
                    </div>
                    <div class="form-group">
                        <label class="form-label">Varianta</label>
                        <div class="radio-group">
                            <label class="radio-option @(_protVariant == "standard" ? "active" : "")">
                                <input type="radio" name="pvar" value="standard" @onchange='() => _protVariant = "standard"' checked="@(_protVariant == "standard")" /> Standard
                            </label>
                            <label class="radio-option @(_protVariant == "plus" ? "active" : "")">
                                <input type="radio" name="pvar" value="plus" @onchange='() => _protVariant = "plus"' checked="@(_protVariant == "plus")" /> Plus
                            </label>
                        </div>
                    </div>
                </div>
                <div class="calc-results">
                    <div class="calc-result-primary">
                        <span class="result-label">Měsíční pojistné</span>
                        <span class="result-value">@_protMonthly.ToString("N0") Kč</span>
                    </div>
                </div>
                <div class="coverage-list">
                    <h4>Standard krytí:</h4>
                    <ul>
                        <li>Úmrtí — pojistitel doplatí zůstatek úvěru</li>
                        <li>Plná invalidita — pojistitel doplatí zůstatek úvěru</li>
                    </ul>
                    @if (_protVariant == "plus")
                    {
                        <h4>Plus krytí navíc:</h4>
                        <ul>
                            <li>Pracovní neschopnost — splátky až 12 měsíců</li>
                            <li>Ztráta zaměstnání — splátky až 12 měsíců</li>
                        </ul>
                    }
                </div>
                <VbButton>Sjednat online</VbButton>
            </ChildContent>
        </ContentCard>
        break;
}

@code {
    private string _subTab = "travel";

    // ── Travel state ──
    private string _travelDest = "europe";
    private string _travelVariant = "standard";
    private int _travelDays = 7;
    private int _travelPersons = 2;
    private decimal _travelPrice => InsuranceCalculator.CalculateTravel(_travelDest, _travelVariant, _travelDays, _travelPersons);

    // ── Property state ──
    private string _propType = "apartment";
    private decimal _propValue = 3_000_000m;
    private bool _propContents;
    private decimal _propAnnual => InsuranceCalculator.CalculatePropertyAnnual(_propType, _propValue, _propContents);
    private decimal _propMonthly => InsuranceCalculator.CalculatePropertyMonthly(_propType, _propValue, _propContents);

    // ── Life state ──
    private int _lifeAge = 30;
    private decimal _lifeCoverage = 1_000_000m;
    private string _lifeVariant = "risk";
    private decimal _lifeMonthly => InsuranceCalculator.CalculateLifeMonthly(_lifeAge, _lifeCoverage, _lifeVariant);

    // ── Payment protection state ──
    private decimal _protPayment = 5_000m;
    private string _protVariant = "standard";
    private decimal _protMonthly => InsuranceCalculator.CalculatePaymentProtection(_protPayment, _protVariant);
}
```

**Step 2: Verify build**

Run: `dotnet build src/FairBank.Web/FairBank.Web.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: implement InsurancePanel — travel, property, life, payment protection"
```

---

### Task 9: Add CSS styles for Products page

**Files:**
- Modify: `src/FairBank.Web.Shared/wwwroot/css/vabank-theme.css` — append product styles

**Step 1: Append CSS to vabank-theme.css**

Add at the end of the file:

```css
/* ═══════════════════════════════════════════════════════════
   PRODUCTS — Tabs, Calculators, Insurance
   ═══════════════════════════════════════════════════════════ */

/* ── Product Main Tabs ── */
.product-tabs {
    display: flex;
    gap: 0;
    background: var(--vb-white);
    border-radius: var(--vb-radius);
    box-shadow: var(--vb-shadow);
    overflow: hidden;
    margin-bottom: 1.5rem;
}

.product-tab {
    flex: 1;
    padding: 1rem;
    border: none;
    background: transparent;
    font-family: var(--vb-font);
    font-size: 0.95rem;
    font-weight: 600;
    color: var(--vb-gray-500);
    cursor: pointer;
    transition: all 0.2s;
    border-bottom: 3px solid transparent;
}

.product-tab:hover { color: var(--vb-red); }

.product-tab.active {
    color: var(--vb-red);
    border-bottom-color: var(--vb-red);
    background: rgba(196, 30, 58, 0.05);
}

.tab-icon { margin-right: 0.5rem; }

/* ── Insurance Sub-tabs ── */
.insurance-subtabs {
    display: flex;
    gap: 0.5rem;
    margin-bottom: 1rem;
    flex-wrap: wrap;
}

.insurance-subtab {
    padding: 0.6rem 1rem;
    border: 2px solid var(--vb-gray-300);
    border-radius: var(--vb-radius-sm);
    background: var(--vb-white);
    font-family: var(--vb-font);
    font-size: 0.85rem;
    font-weight: 600;
    color: var(--vb-gray-700);
    cursor: pointer;
    transition: all 0.2s;
}

.insurance-subtab:hover { border-color: var(--vb-red); color: var(--vb-red); }

.insurance-subtab.active {
    border-color: var(--vb-red);
    background: var(--vb-red);
    color: var(--vb-white);
}

/* ── Calculator Form ── */
.calculator-form {
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
    margin-bottom: 1.5rem;
}

.calculator-form input[type="range"] {
    width: 100%;
    accent-color: var(--vb-red);
    height: 6px;
    cursor: pointer;
}

.range-value {
    text-align: right;
    font-size: 1.1rem;
    font-weight: 700;
    color: var(--vb-black);
    margin-top: 0.25rem;
}

/* ── Fixation Buttons ── */
.fixation-options { display: flex; gap: 0.5rem; }

.fixation-btn {
    flex: 1;
    padding: 0.6rem 1rem;
    border: 2px solid var(--vb-gray-300);
    border-radius: var(--vb-radius-sm);
    background: var(--vb-white);
    font-family: var(--vb-font);
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s;
}

.fixation-btn:hover { border-color: var(--vb-red); }

.fixation-btn.active {
    border-color: var(--vb-red);
    background: var(--vb-red);
    color: var(--vb-white);
}

/* ── Calculator Results ── */
.calc-results {
    background: linear-gradient(135deg, var(--vb-gray-100) 0%, var(--vb-white) 100%);
    border: 2px solid var(--vb-gray-100);
    border-radius: var(--vb-radius);
    padding: 1.5rem;
    margin-bottom: 1.5rem;
}

.calc-result-primary {
    text-align: center;
    margin-bottom: 1rem;
}

.calc-result-primary .result-label {
    display: block;
    font-size: 0.85rem;
    color: var(--vb-gray-500);
    text-transform: uppercase;
    letter-spacing: 0.05em;
    margin-bottom: 0.25rem;
}

.calc-result-primary .result-value {
    font-size: 2.5rem;
    font-weight: 800;
    color: var(--vb-green);
}

.calc-results-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
    gap: 1rem;
}

.calc-result-item {
    text-align: center;
    padding: 0.5rem;
}

.calc-result-item .result-label {
    display: block;
    font-size: 0.75rem;
    color: var(--vb-gray-500);
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.result-value-sm {
    font-size: 1.2rem;
    font-weight: 700;
    color: var(--vb-black);
}

/* ── Radio Group ── */
.radio-group { display: flex; gap: 0.5rem; }

.radio-option {
    flex: 1;
    padding: 0.75rem 1rem;
    border: 2px solid var(--vb-gray-300);
    border-radius: var(--vb-radius-sm);
    text-align: center;
    cursor: pointer;
    font-weight: 600;
    transition: all 0.2s;
}

.radio-option input { display: none; }

.radio-option:hover { border-color: var(--vb-red); }

.radio-option.active {
    border-color: var(--vb-red);
    background: rgba(196, 30, 58, 0.05);
    color: var(--vb-red);
}

/* ── Checkbox ── */
.checkbox-label {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    font-weight: 500;
    cursor: pointer;
}

.checkbox-label input[type="checkbox"] {
    width: 1.2rem;
    height: 1.2rem;
    accent-color: var(--vb-red);
}

/* ── Coverage List ── */
.coverage-list {
    background: var(--vb-gray-100);
    border-radius: var(--vb-radius-sm);
    padding: 1rem 1.25rem;
    margin: 1rem 0;
}

.coverage-list h4 {
    font-size: 0.85rem;
    text-transform: uppercase;
    color: var(--vb-gray-700);
    margin-bottom: 0.5rem;
}

.coverage-list ul { list-style: none; padding: 0; }

.coverage-list li {
    padding: 0.3rem 0;
    padding-left: 1.5rem;
    position: relative;
    font-size: 0.9rem;
}

.coverage-list li::before {
    content: "✓";
    position: absolute;
    left: 0;
    color: var(--vb-green);
    font-weight: 700;
}

/* ── Representative Example ── */
.representative-example {
    font-size: 0.75rem;
    color: var(--vb-gray-500);
    border-top: 1px solid var(--vb-gray-100);
    padding-top: 1rem;
    margin-top: 1rem;
    line-height: 1.6;
}

/* ── Modal ── */
.modal-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
}

.modal-card {
    background: var(--vb-white);
    border-radius: var(--vb-radius);
    padding: 2rem;
    max-width: 400px;
    width: 90%;
    text-align: center;
    box-shadow: var(--vb-shadow-lg);
}

.modal-card h3 { margin-bottom: 1rem; color: var(--vb-green); }
.modal-card p { margin-bottom: 1.5rem; color: var(--vb-gray-700); }
```

**Step 2: Verify build**

Run: `dotnet build src/FairBank.Web/FairBank.Web.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add CSS for product tabs, calculators, insurance, modals"
```

---

### Task 10: Run all tests, Docker build, and visual verification

**Step 1: Run all product tests**

Run: `dotnet test tests/FairBank.Web.Products.Tests/ -v m`
Expected: All tests pass (LoanCalculator, MortgageCalculator, InsuranceCalculator)

**Step 2: Docker rebuild**

Run: `docker compose up --build -d`
Expected: All containers start successfully

**Step 3: Visual verification with browser**

- Navigate to `http://localhost/produkty`
- Verify tab switching works (Osobní úvěr / Hypotéka / Pojištění)
- Verify loan calculator sliders update results in real-time
- Verify mortgage LTV limit enforced
- Verify all 4 insurance sub-tabs render
- Verify "Požádat" modals open and close

**Step 4: Final commit and push**

```bash
git push origin feature/loans-and-insurance
```
