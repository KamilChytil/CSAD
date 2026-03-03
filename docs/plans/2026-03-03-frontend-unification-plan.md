# Frontend Unification Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace all remaining emoji icons with VbIcon SVG components and remove dead AuthStateService code.

**Architecture:** Pure frontend changes — swap emoji strings for `<VbIcon>` Blazor components in .razor files, add missing icon SVGs to VbIcon.razor, delete unused auth service.

**Tech Stack:** Blazor WASM, Razor components, SVG icons

---

### Task 1: Add missing icons to VbIcon.razor

**Files:**
- Modify: `src/FairBank.Web.Shared/Components/VbIcon.razor` — add 7 new icon entries to GetSvg switch

**Step 1: Add new icon entries before the `_ =>` default case**

Add these entries to the `GetSvg` switch expression in VbIcon.razor:

```csharp
"exchange" => "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='10'/><path d='M2 12h20'/><path d='M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z'/><polyline points='7 9 5 7 7 5'/><polyline points='17 15 19 17 17 19'/></svg>",
"heart" => "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z'/></svg>",
"lock" => "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><rect x='3' y='11' width='18' height='11' rx='2' ry='2'/><path d='M7 11V7a5 5 0 0 1 10 0v4'/></svg>",
"eye" => "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z'/><circle cx='12' cy='12' r='3'/></svg>",
"eye-off" => "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24'/><line x1='1' y1='1' x2='23' y2='23'/></svg>",
"warning" => "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z'/><line x1='12' y1='9' x2='12' y2='13'/><line x1='12' y1='17' x2='12.01' y2='17'/></svg>",
"check" => "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><polyline points='20 6 9 17 4 12'/></svg>",
"x-circle" => "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='10'/><line x1='15' y1='9' x2='9' y2='15'/><line x1='9' y1='9' x2='15' y2='15'/></svg>",
"ban" => "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='10'/><line x1='4.93' y1='4.93' x2='19.07' y2='19.07'/></svg>",
"building" => "<svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><rect x='4' y='2' width='16' height='20' rx='1'/><path d='M9 22V12h6v10'/><path d='M8 6h.01M16 6h.01M12 6h.01M8 10h.01M16 10h.01M12 10h.01'/></svg>",
```

**Step 2: Commit**

```
git add src/FairBank.Web.Shared/Components/VbIcon.razor
git commit -m "feat(icons): add exchange, heart, lock, eye, warning, check icons to VbIcon"
```

---

### Task 2: Fix navigation — exchange icon

**Files:**
- Modify: `src/FairBank.Web/Layout/SideNav.razor:38` — replace `💱` with VbIcon
- Modify: `src/FairBank.Web/Layout/BottomNav.razor:23` — replace `💱` with VbIcon

**Step 1: In both files, replace:**

```razor
<!-- OLD -->
<span class="side-nav-icon">💱</span>
<!-- NEW -->
<span class="side-nav-icon"><VbIcon Name="exchange" Size="sm" /></span>
```

Same for BottomNav (uses `nav-icon` class instead of `side-nav-icon`).

**Step 2: Commit**

```
git add src/FairBank.Web/Layout/SideNav.razor src/FairBank.Web/Layout/BottomNav.razor
git commit -m "feat(nav): replace exchange emoji with VbIcon in SideNav and BottomNav"
```

---

### Task 3: Fix Products tabs

**File:**
- Modify: `src/FairBank.Web.Products/Pages/Products.razor:13,17,21,27`

**Step 1: Replace 4 emoji tab icons:**

```razor
<!-- line 13: OLD --> <span class="tab-icon">💰</span> Osobní úvěr
<!-- line 13: NEW --> <span class="tab-icon"><VbIcon Name="wallet" Size="sm" /></span> Osobní úvěr

<!-- line 17: OLD --> <span class="tab-icon">🏠</span> Hypotéka
<!-- line 17: NEW --> <span class="tab-icon"><VbIcon Name="home" Size="sm" /></span> Hypotéka

<!-- line 21: OLD --> <span class="tab-icon">🛡️</span> Pojištění
<!-- line 21: NEW --> <span class="tab-icon"><VbIcon Name="shield" Size="sm" /></span> Pojištění

<!-- line 27: OLD --> <span class="tab-icon">📋</span> Moje produkty
<!-- line 27: NEW --> <span class="tab-icon"><VbIcon Name="clipboard" Size="sm" /></span> Moje produkty
```

**Step 2: Commit**

```
git commit -m "feat(products): replace emoji tab icons with VbIcon"
```

---

### Task 4: Fix Insurance sub-tabs and radio labels

**File:**
- Modify: `src/FairBank.Web.Products/Components/InsurancePanel.razor:8,10,12,14,27,29,30,90,93`

**Step 1: Replace 4 sub-tab emojis:**

```razor
<!-- line 8: OLD  --> ✈️ Cestovní
<!-- line 8: NEW  --> <VbIcon Name="travel" Size="sm" /> Cestovní

<!-- line 10: OLD --> 🏠 Nemovitost
<!-- line 10: NEW --> <VbIcon Name="home" Size="sm" /> Nemovitost

<!-- line 12: OLD --> ❤️ Životní
<!-- line 12: NEW --> <VbIcon Name="heart" Size="sm" /> Životní

<!-- line 14: OLD --> 🔒 Ochrana splátek
<!-- line 14: NEW --> <VbIcon Name="lock" Size="sm" /> Ochrana splátek
```

**Step 2: Replace radio label emojis:**

```razor
<!-- line 27: OLD --> 🇪🇺 Evropa   → <VbIcon Name="travel" Size="sm" /> Evropa
<!-- line 30: OLD --> 🌍 Svět      → <VbIcon Name="travel" Size="sm" /> Svět
<!-- line 90: OLD --> 🏢 Byt       → <VbIcon Name="building" Size="sm" /> Byt
<!-- line 93: OLD --> 🏡 Dům       → <VbIcon Name="home" Size="sm" /> Dům
```

**Step 3: Commit**

```
git commit -m "feat(insurance): replace emoji icons with VbIcon in sub-tabs and radio labels"
```

---

### Task 5: Fix MyProductsPanel and Management — product/status icons

**Files:**
- Modify: `src/FairBank.Web.Products/Components/MyProductsPanel.razor:33,38,114-122,136-142`
- Modify: `src/FairBank.Web.Products/Pages/Management.razor:22,35,39,75,117-121,286-294`

**Step 1: In MyProductsPanel.razor — change GetProductIcon to return VbIcon name string:**

```csharp
// OLD:
private static string GetProductIcon(string productType) => productType switch
{
    "PersonalLoan" => "💰",
    "Mortgage" => "🏠",
    ...
};

// NEW:
private static string GetProductIconName(string productType) => productType switch
{
    "PersonalLoan" => "wallet",
    "Mortgage" => "home",
    "TravelInsurance" => "travel",
    "PropertyInsurance" => "home",
    "LifeInsurance" => "heart",
    "PaymentProtection" => "shield",
    _ => "clipboard"
};
```

Update usage at line 33 from `@GetProductIcon(app.ProductType)` to `<VbIcon Name="@GetProductIconName(app.ProductType)" Size="sm" />`

**Step 2: Change GetStatusLabel to remove emojis:**

```csharp
// OLD:
private static string GetStatusLabel(string status) => status switch
{
    "Pending" => "⏳ Čeká na schválení",
    "Active" => "✅ Aktivní",
    "Rejected" => "❌ Zamítnuto",
    "Cancelled" => "🚫 Zrušeno",
    _ => status
};

// NEW:
private static string GetStatusLabel(string status) => status switch
{
    "Pending" => "Čeká na schválení",
    "Active" => "Aktivní",
    "Rejected" => "Zamítnuto",
    "Cancelled" => "Zrušeno",
    _ => status
};
```

Add a new helper for the status icon name:

```csharp
private static string GetStatusIconName(string status) => status switch
{
    "Pending" => "clock",
    "Active" => "check",
    "Rejected" => "x-circle",
    "Cancelled" => "ban",
    _ => "clipboard"
};
```

Update line 38 to render: `<VbIcon Name="@GetStatusIconName(app.Status)" Size="sm" /> @GetStatusLabel(app.Status)`

**Step 3: Apply same pattern to Management.razor:**
- Same `GetProductIconName` rename + VbIcon usage at lines 35, 75
- Replace `⏳ Čeká na schválení` at line 39 with `<VbIcon Name="clock" Size="sm" /> Čeká na schválení`
- Replace `✅ Všechny žádosti` at line 22 with `<VbIcon Name="check" Size="sm" /> Všechny žádosti`
- Replace `✅ Schválit` at line 118 with `<VbIcon Name="check" Size="sm" /> Schválit`
- Replace `❌ Zamítnout` at line 121 with `<VbIcon Name="x-circle" Size="sm" /> Zamítnout`

**Step 4: Commit**

```
git commit -m "feat(products): replace product/status emojis with VbIcon in MyProductsPanel and Management"
```

---

### Task 6: Fix ChatList icons

**File:**
- Modify: `src/FairBank.Web/Pages/ChatList.razor:11,134-136`

**Step 1: Replace heading emoji:**

```razor
<!-- line 11: OLD --> <h2>💬 Zprávy</h2>
<!-- line 11: NEW --> <h2><VbIcon Name="chat" Size="sm" /> Zprávy</h2>
```

**Step 2: Change GetIcon to return VbIcon name + update usage:**

```csharp
// OLD:
private static string GetIcon(string type) => type switch
{
    "Support" => "🏦",
    "Family" => "👨‍👩‍👧",
    _ => "💬"
};

// NEW:
private static string GetIconName(string type) => type switch
{
    "Support" => "bank",
    "Family" => "user",
    _ => "chat"
};
```

Update the icon rendering from `@GetIcon(...)` to `<VbIcon Name="@GetIconName(...)" Size="sm" />`

**Step 3: Commit**

```
git commit -m "feat(chat): replace emoji icons with VbIcon in ChatList"
```

---

### Task 7: Fix Login/Register alert icons and password toggle

**Files:**
- Modify: `src/FairBank.Web.Auth/Pages/Login.razor:24,35,43,73`
- Modify: `src/FairBank.Web.Auth/Pages/Register.razor:24,196,221`

**Step 1: Replace alert icon emojis in Login.razor:**

```razor
<!-- line 24: OLD --> <span class="alert-icon">🔒</span>
<!-- line 24: NEW --> <span class="alert-icon"><VbIcon Name="lock" Size="sm" /></span>

<!-- line 35: OLD --> <span class="alert-icon">⚠️</span>
<!-- line 35: NEW --> <span class="alert-icon"><VbIcon Name="warning" Size="sm" /></span>

<!-- line 43: OLD --> <span class="alert-icon">⏰</span>
<!-- line 43: NEW --> <span class="alert-icon"><VbIcon Name="clock" Size="sm" /></span>
```

**Step 2: Replace password toggle emojis in Login.razor:**

```razor
<!-- line 73: OLD --> @(_showPassword ? "🙈" : "👁️")
<!-- line 73: NEW --> <VbIcon Name="@(_showPassword ? "eye-off" : "eye")" Size="sm" />
```

**Step 3: Apply same pattern to Register.razor:**
- Line 24: `✅` → `<VbIcon Name="check" Size="sm" />`
- Lines 196, 221: password toggles same as Login

**Step 4: Commit**

```
git commit -m "feat(auth): replace emoji alert and password icons with VbIcon"
```

---

### Task 8: Cleanup dead AuthStateService code

**Files:**
- Modify: `src/FairBank.Web/Program.cs:20,26-28` — remove 3 lines
- Delete: `src/FairBank.Web.Shared/Services/AuthStateService.cs`
- Delete: `src/FairBank.Web/Pages/Login.razor` (legacy `/login-legacy` page)

**Step 1: Remove from Program.cs:**

Delete these 3 lines:
```csharp
builder.Services.AddSingleton<AuthStateService>();   // line 20

var authState = host.Services.GetRequiredService<AuthStateService>();  // line 26
await authState.InitializeAsync(js);                                    // line 28
```

Also remove the `using Microsoft.JSInterop;` import if it's only used for the js variable (check first — if IJSRuntime is used elsewhere keep it).

**Step 2: Delete AuthStateService.cs:**

```bash
rm src/FairBank.Web.Shared/Services/AuthStateService.cs
```

**Step 3: Delete legacy Login.razor:**

```bash
rm src/FairBank.Web/Pages/Login.razor
```

**Step 4: Verify build**

```bash
docker run --rm -v $(pwd):/app -w /app mcr.microsoft.com/dotnet/sdk:10.0-preview dotnet build src/FairBank.Web/FairBank.Web.csproj
```

**Step 5: Commit**

```
git commit -m "chore: remove dead AuthStateService and legacy login page"
```

---

### Task 9: Docker rebuild and visual verification

**Step 1: Rebuild and start**

```bash
docker compose build web-app --no-cache
docker compose up -d web-app
```

**Step 2: Verify all pages visually**
- Login page — alert icons are SVG, password toggle is SVG eye
- Overview — nav icons all SVG including Exchange
- Products — 4 tab icons are SVG
- Insurance — 4 sub-tab icons are SVG, radio labels use SVG
- Moje produkty — product type icons and status badges are SVG
- Správa (as banker) — approve/reject buttons have SVG icons
- Chat — heading and conversation icons are SVG

**Step 3: Push**

```bash
git push origin feature/loans-and-insurance
```
