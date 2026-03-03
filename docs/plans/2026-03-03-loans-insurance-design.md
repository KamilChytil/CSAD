# Loans & Insurance Design — FairBank.Web.Products

## Overview

Single Blazor WASM module `FairBank.Web.Products` adding a "Produkty" section with three main tabs: Osobní úvěr, Hypotéka, Pojištění. All calculations frontend-only (no backend service). Same pattern as `FairBank.Web.Exchange`.

## Navigation

- Nav item: `🏦 Produkty` at route `/produkty`
- One entry in both SideNav and BottomNav

## Page Structure

```
[💰 Osobní úvěr]  [🏠 Hypotéka]  [🛡️ Pojištění]
─────────────────────────────────────────────────
       Active tab content (calculator + info)
```

## Tab 1: Osobní úvěr (Personal Loan)

### Calculator Inputs
- **Částka** (Amount): slider + input, 10,000–2,000,000 CZK, default 200,000
- **Doba splácení** (Term): slider + input, 6–96 months, default 60

### Interest Rate Tiers
| Amount Range | Rate |
|---|---|
| up to 100,000 | 8.9% p.a. |
| 100,001–500,000 | 5.9% p.a. |
| 500,001–1,000,000 | 5.4% p.a. |
| 1,000,001+ | 4.9% p.a. |

### Outputs (real-time)
- **Měsíční splátka** — annuity formula
- **Úroková sazba** — p.a.
- **RPSN** — rate + 0.2%
- **Celkem zaplatíte** — payment × months

### Additional
- Representative example block (legally required pattern)
- CTA "Požádat o úvěr" → modal confirmation

## Tab 2: Hypotéka (Mortgage)

### Calculator Inputs
- **Cena nemovitosti** (Property price): slider + input, 500,000–20,000,000 CZK, default 4,000,000
- **Výše úvěru** (Loan amount): slider + input, max 80% of property price, default 3,200,000
- **Doba splácení** (Term): slider + input, 5–30 years, default 25
- **Fixace** (Fixation): dropdown — 1, 3, 5, 10 years

### Rate Matrix (fixation × LTV)
| Fixation | LTV ≤60% | LTV 60–80% |
|---|---|---|
| 1 year | 5.29% | 5.59% |
| 3 years | 4.89% | 5.19% |
| 5 years | 4.49% | 4.79% |
| 10 years | 4.99% | 5.29% |

### Outputs (real-time)
- **Měsíční splátka** — annuity
- **Úroková sazba** — from matrix
- **Vlastní zdroje** — property price minus loan
- **Celkem zaplatíte** — payment × months

### Additional
- Representative example block
- CTA "Požádat o hypotéku" → modal

## Tab 3: Pojištění (Insurance)

Four sub-tabs within the insurance panel:

### 3a) Cestovní pojištění (Travel Insurance)
- **Destinace**: radio — Evropa / Svět
- **Počet dní**: input 1–90
- **Počet osob**: input 1–6
- **Varianta**: radio — Standard / Plus

Pricing per day per person:
| | Standard | Plus |
|---|---|---|
| Evropa | 35 CZK | 65 CZK |
| Svět | 75 CZK | 120 CZK |

Output: total price + coverage summary

### 3b) Pojištění nemovitosti (Property Insurance)
- **Typ**: radio — Byt / Dům
- **Hodnota nemovitosti**: slider 500,000–15,000,000
- **Včetně domácnosti**: checkbox

Pricing: 0.08% of value/year (apartment), 0.12% (house), +40% if contents included

Output: annual + monthly premium, coverage list

### 3c) Životní pojištění (Life Insurance)
- **Věk**: input 18–65
- **Pojistná částka**: slider 200,000–5,000,000, default 1,000,000
- **Varianta**: radio — Rizikové / Investiční

Pricing: base rate × age coefficient × coverage amount

Output: monthly premium + coverage summary

### 3d) Ochrana splátek (Payment Protection)
- **Měsíční splátka**: input (auto-filled from loan/mortgage if available)
- **Varianta**: radio — Standard (death + disability) / Plus (+ illness + job loss)

Pricing: Standard = 5.5% of payment, Plus = 8.5% of payment

Output: monthly premium + coverage list

## Technical Architecture

### Project Structure
```
src/FairBank.Web.Products/
├── FairBank.Web.Products.csproj
├── _Imports.razor
├── Pages/
│   └── Products.razor              ← main page @page "/produkty"
├── Components/
│   ├── LoanCalculator.razor
│   ├── MortgageCalculator.razor
│   └── InsurancePanel.razor         ← contains 4 sub-tabs
│       ├── TravelInsurance (inline or partial)
│       ├── PropertyInsurance
│       ├── LifeInsurance
│       └── PaymentProtection
└── Services/
    ├── LoanCalculator.cs            ← annuity math + rate tiers
    ├── MortgageCalculator.cs        ← annuity + LTV + fixation matrix
    └── InsuranceCalculator.cs       ← all insurance pricing logic
```

### Integration Points
- Add to `FairBank.Web.csproj` as ProjectReference
- Add assembly to `App.razor` router
- Add to Dockerfile COPY steps
- Add nav items to `BottomNav.razor` and `SideNav.razor`
- Add CSS to `vabank-theme.css`

### Testing
- Unit tests for all calculator services (annuity, rate tiers, insurance pricing)
- `tests/FairBank.Web.Products.Tests/`
