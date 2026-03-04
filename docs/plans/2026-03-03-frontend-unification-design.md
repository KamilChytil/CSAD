# Frontend Unification Design

## Problem

After merging main's new UI (VbIcon SVG components, ThemeService) into the feature branch, several components still use emoji icons instead of VbIcon. This creates visual inconsistency between pages that were updated in main (Overview, Payments, Savings, Investments, Profile) and pages we built on the feature branch (Products, Insurance, Management, Chat).

## Scope — 8 Changes

### 1. Navigation — Exchange icon

**Files:** `SideNav.razor`, `BottomNav.razor`
**Change:** Replace `💱` emoji with `<VbIcon Name="exchange" Size="sm" />` (new icon to add).

### 2. Product tabs

**File:** `Products.razor`
**Change:** Replace 4 emoji tab icons with VbIcon:
- `💰` Osobni uver → `<VbIcon Name="wallet" Size="sm" />`
- `🏠` Hypoteka → `<VbIcon Name="home" Size="sm" />`
- `🛡️` Pojisteni → `<VbIcon Name="shield" Size="sm" />`
- `📋` Moje produkty → `<VbIcon Name="clipboard" Size="sm" />`

### 3. Insurance sub-tabs

**File:** `InsurancePanel.razor`
**Change:** Replace 4 emoji sub-tab icons:
- `✈️` Cestovni → `<VbIcon Name="travel" Size="sm" />`
- `🏠` Nemovitost → `<VbIcon Name="home" Size="sm" />`
- `❤️` Zivotni → `<VbIcon Name="heart" Size="sm" />` (new icon)
- `🔒` Ochrana splatek → `<VbIcon Name="lock" Size="sm" />` (new icon)

Also replace radio label emojis with VbIcon where applicable.

### 4. MyProductsPanel + Management — product/status icons

**Files:** `MyProductsPanel.razor`, `Management.razor`
**Change:** Refactor `GetProductIcon()` and `GetStatusLabel()` from returning emoji strings to rendering `<VbIcon>` components. Add `check`, `warning` icons.

### 5. ChatList icons

**File:** `ChatList.razor`
**Change:** Replace `💬` heading and `GetIcon()` emoji returns with VbIcon components.

### 6. Login/Register alert and toggle icons

**Files:** `Auth/Pages/Login.razor`, `Auth/Pages/Register.razor`
**Change:** Replace alert emojis (`🔒⚠️⏰✅`) with VbIcon (`shield`, `warning`, `clock`, `check`). Replace password toggle (`🙈👁️`) with VbIcon `eye`/`eye-off` (new icons).

### 7. Exchange swap button

**File:** `Exchange.razor`
**Change:** Replace `⇄` character with `<VbIcon Name="transfer" Size="sm" />`.

### 8. Dead code cleanup

- **Program.cs:** Remove `AuthStateService` registration and initialization (3 lines).
- **AuthStateService.cs:** Delete file.
- **Pages/Login.razor (legacy):** Delete `/login-legacy` page that uses `AuthStateService`.

## New VbIcon Icons Required

Add to `VbIcon.razor`: `exchange`, `heart`, `lock`, `eye`, `eye-off`, `warning`, `check`.

## Testing

- Docker rebuild + visual verification of all affected pages
- Existing 68 unit tests remain unaffected (backend only)
