# In-App Smenarna (Currency Exchange) Design

## Overview

Predelat stavajici stranku `/kurzy` na plnohodnotnou smenarnu (in-app exchange) inspirovanou modernnimi bankami (KB, MONETA, Partners Banka). Uzivatel vidi kurzy, vybere menovy par, zada castku a provede realny prevod mezi svymi ucty.

## Pozadavky

- Dropdown se vsemi ~50 fiat menami z API (pro zobrazeni kurzu)
- Smena jen mezi 4 menami uctu: CZK, EUR, USD, GBP
- Kurz se nacte po vyberu paru, auto-refresh co 10 sekund
- Tlacitko "Prevest" provede realny prevod (debit + credit)
- Oblibene smeny (ulozene pary men)
- Historie smen (posledni provedene smeny)
- Potvrzovaci dialog pred provedenim smeny

## API Provider

**fawazahmed0/currency-api** (stavajici, free, bez API klice)
- Primary: `https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/czk.json`
- Fallback: `https://latest.currency-api.pages.dev/v1/currencies/czk.json`
- Aktualizace: 1x denne
- Filtrovano na ~50 fiat men

## Architektura

### Pristup: Rozsireni Payments Microservice

Neni novy microservice — exchange logika se prida do stavajiciho `FairBank.Payments` service. Duvodem je mensi scope a dostatecna koherence (smena = typ platby).

### Backend Endpointy

| Endpoint | Metoda | Popis |
|---|---|---|
| `/api/v1/exchange/rate` | GET | Aktualni kurz (`?from=CZK&to=EUR`) |
| `/api/v1/exchange/convert` | POST | Provede smenu mezi ucty |
| `/api/v1/exchange/history` | GET | Historie smen prihlaseneho uzivatele |
| `/api/v1/exchange/favorites` | GET | Oblibene menove pary uzivatele |
| `/api/v1/exchange/favorites` | POST | Pridat oblibeny par |
| `/api/v1/exchange/favorites/{id}` | DELETE | Odebrat oblibeny par |

### CQRS Commands & Queries

- `GetExchangeRateQuery` — nacte kurz z cache/API
- `ExecuteExchangeCommand` — provede smenu (validace kurzu + debit/credit)
- `GetExchangeHistoryQuery` — historie smen uzivatele
- `GetFavoritesQuery` — oblibene pary
- `AddFavoriteCommand` — pridat oblibeny par
- `RemoveFavoriteCommand` — odebrat oblibeny par

### ExchangeRateService

- Stahuje kurzy z fawazahmed0 API
- Cachuje v `IMemoryCache` (TTL 60 sekund)
- Stahne cely JSON jednou, pak servuje jednotlive pary z cache
- Fallback URL pokud primary selze

### Integrace s Accounts Service

Smena = dve operace:
1. Debit ze zdrojoveho uctu (volani Accounts API)
2. Credit na cilovy ucet (volani Accounts API)

Provadi se v `ExecuteExchangeCommandHandler`. Castka se prepocita podle aktualniho kurzu z cache.

## Databazovy Model

V `payments_service` schema.

### exchange_transactions

| Sloupec | Typ | Popis |
|---|---|---|
| id | UUID PK | |
| user_id | UUID | Kdo smenil |
| source_account_id | UUID | Zdrojovy ucet |
| target_account_id | UUID | Cilovy ucet |
| from_currency | VARCHAR(3) | Zdrojova mena (ISO 4217) |
| to_currency | VARCHAR(3) | Cilova mena (ISO 4217) |
| source_amount | DECIMAL(18,2) | Castka odectena |
| target_amount | DECIMAL(18,2) | Castka pripsana |
| exchange_rate | DECIMAL(18,6) | Pouzity kurz |
| created_at | TIMESTAMP | Kdy probehla smena |

### exchange_favorites

| Sloupec | Typ | Popis |
|---|---|---|
| id | UUID PK | |
| user_id | UUID | |
| from_currency | VARCHAR(3) | |
| to_currency | VARCHAR(3) | |
| created_at | TIMESTAMP | |

## Frontend Design

### Stranka: Predelani stavajici `/kurzy` (Exchange.razor)

Pouziva stavajici VA-BANK theme komponenty: `PageHeader`, `ContentCard`, `VbButton`, `VbIcon`, `form-input`, `form-label`.

### Layout

```
PageHeader: "SMENARNA"

ContentCard: "Smena men"
  - Dropdown "Z meny" (vsechny ~50 fiat men s nazvy a vlajkami)
  - Dropdown "Na menu" (vsechny ~50 fiat men)
  - Tlacitko "Prohodit" (VbButton ghost)
  - Input "Castka" (form-input)
  - Info box: kurz, cilova castka, cas aktualizace + odpocet
  - Tlacitko "Prevest" (VbButton primary)
    - Aktivni jen pokud obe meny jsou z CZK/EUR/USD/GBP
    - Pro ostatni meny se zobrazi jen informativni prepocet

ContentCard: "Oblibene smeny"
  - Chipy/tagy s ulozenymi pary (VbButton outline)
  - Klik = predvyplni From/To
  - Tlacitko "+ Pridat" pro ulozeni aktualniho paru

ContentCard: "Historie smen"
  - Seznam poslednich smen (datum, castky, kurz)
  - Podobny styl jako TransactionItem
```

### UX Flow

1. Uzivatel vybere zdrojovou menu z dropdownu
2. Vybere cilovou menu
3. Po vyberu obou men: `GET /api/v1/exchange/rate` → zobrazi kurz
4. Kurz se auto-refreshuje co 10 sekund (odpocet viditelny)
5. Uzivatel zada castku → okamzity prepocet cilove castky
6. Klikne "Prevest" → potvrzovaci dialog s finalnim kurzem
7. Po potvrzeni: `POST /api/v1/exchange/convert`
8. Uspech: zaznam se prida do historie, zobrazeni potvrzeni

### Omezeni smeny

- Prevod mozny jen mezi CZK, EUR, USD, GBP (meny uctu)
- Pro ostatni meny (~46) se zobrazi jen informativni kurz a prepocet
- Uzivatel musi mit ucty v obou menach

## API Gateway

Nova route v `appsettings.json`:

```json
"exchange-route": {
  "ClusterId": "payments-cluster",
  "Match": {
    "Path": "/api/v1/exchange/{**catch-all}"
  }
}
```

## Meny

- Backend: stavajici `Currency` enum (CZK, EUR, USD, GBP) — bez zmeny
- Frontend dropdown: vsech ~50 fiat men z `CurrencyConverter.CurrencyNames` (uz existuje)
- Smena mozna jen mezi 4 menami enumu
- Zobrazeni kurzu pro vsechny meny

## Bezpecnost

- Kurz se validuje na backendu (klient nemuze poslat vlastni kurz)
- Backend cachuje kurzy a pouziva svuj cache pri smene
- Autentizace pres stavajici JWT session tokeny
- Uzivatel muze smenovat jen mezi svymi ucty
