# Merge: Ondra2-Exporty → main (Statement Export Feature)

**Date:** 2026-03-04  
**Source branch:** `origin/Ondra2-Exporty` (commit `0ac58bc`, author: ondra5555010)  
**Merge base:** `a593b34`

---

## Co bylo přidáno (nová funkcionalita)

### 1. Nová mikroslužba: `FairBank.Documents`

Nová sada projektů v `src/Services/Documents/`:

| Projekt | Účel |
|---|---|
| `FairBank.Documents.Domain` | Prázdná doménová vrstva (SharedKernel reference) |
| `FairBank.Documents.Application` | MediatR commandy + porty (interfaces) |
| `FairBank.Documents.Infrastructure` | HTTP klienti, generátory |
| `FairBank.Documents.Api` | Minimální API, endpointy, Dockerfile |

**Endpoint:** `GET /api/v1/documents/statements/{accountId}?from=&to=&format=[pdf|docx|xlsx]`

- Vrací binární soubor výpisu transakcí za zvolené období
- Formáty: `pdf` (text-based), `docx` (OpenXml), `xlsx` (ClosedXML)
- Čerpá transakce z Accounts service přes HTTP (`GET /api/v1/accounts/{id}/transactions`)

**Přidané typy:**  
- `IStatementGenerator` / `StatementGenerator` — generuje výpisy  
- `IContractGenerator` / `ContractGenerator` — generuje smlouvy (stub, zatím není exposed v API)  
- `IAccountsServiceClient` / `AccountsServiceHttpClient` — HTTP klient na Accounts  
- `IProductsServiceClient` / `ProductsServiceHttpClient` — HTTP klient na Products (stub, potřebný pro build)

### 2. GetAccountTransactions (Accounts service)

Nový query handler a endpoint v `FairBank.Accounts`:
- `GetAccountTransactionsQuery(Guid AccountId, DateTime? From, DateTime? To)`
- Endpoint: `GET /api/v1/accounts/{accountId}/transactions?from=&to=`
- Čte transakce z Marten event store

### 3. Frontend: tlačítko výpisu v pohyby view

V `FairBank.Web.Payments/Pages/Payments.razor` (sekce `_pageView == "pohyby"`):
- Dvě datová pole: **Od** / **Do** (`InputDate`)
- Výběr formátu: PDF / DOCX / Excel
- Tlačítko **Stáhnout výpis** — zavolá `DownloadStatementAsync` → JS interop `vabank.downloadFile(base64, filename)`

Nové fieldy:
```csharp
private DateTime? _statementFrom;
private DateTime? _statementTo;
private string _statementFormat = "pdf";
```

Metoda:
```csharp
private async Task DownloadStatement()
{
    var bytes = await Api.DownloadStatementAsync(_selectedAccountId.Value, _statementFrom, _statementTo, _statementFormat);
    var base64 = Convert.ToBase64String(bytes);
    await Js.InvokeVoidAsync("vabank.downloadFile", base64, $"vypis-{DateTime.UtcNow:yyyyMMdd}.{_statementFormat}");
}
```

### 4. API Gateway

Přidat route a cluster v `src/FairBank.ApiGateway/appsettings.json`:
```json
"documents-route": { "ClusterId": "documents-cluster", "Match": { "Path": "/api/v1/documents/{**catch-all}" } }
"documents-cluster": { "Destinations": { "documents-api": { "Address": "http://documents-api:8080" } } }
```

### 5. JavaScript interop

V `src/FairBank.Web/wwwroot/js/vabank-interop.js`:
```js
downloadFile(base64Data, fileName) {
    const link = document.createElement('a');
    link.href = 'data:application/octet-stream;base64,' + base64Data;
    link.download = fileName;
    link.click();
}
```

### 6. docker-compose.yml

Přidána služba `documents-api`:
```yaml
documents-api:
  build: { context: ., dockerfile: src/Services/Documents/FairBank.Documents.Api/Dockerfile }
  container_name: fairbank-documents-api
  expose: ["8080"]
  environment:
    ASPNETCORE_ENVIRONMENT: Development
    Services__AccountsApi: "http://accounts-api:8080"
  depends_on: { accounts-api: { condition: service_started } }
  networks: [backend]
```

**Poznámka:** Větev Ondra2-Exporty měnila port pro `web-app` na `8080:80`. Při merge bylo opraveno zpět na `80:80`.

---

## Co bylo fixnuto při merge

| Problém | Řešení |
|---|---|
| `IProductsServiceClient` nebyl definován (compile error v `GenerateContractCommandHandler`) | Vytvořen interface `IProductsServiceClient` v Ports, stub `ProductsServiceHttpClient` v Infrastructure |
| `IContractGenerator` registrace chyběla v DI | Přidán `ContractGenerator` stub, zaregistrován v `DependencyInjection.cs` |
| docker-compose měnil port z `80:80` na `8080:80` | Opraveno zpět na `80:80` |
| Chyba stahování výpisu mlčky ignorována | `catch` blok nastaven na zobrazení `_errorMessage` |
| Jméno staženého souboru bylo GUID | Změněno na `vypis-yyyyMMdd.{format}` |

---

## Jak to funguje (end-to-end flow)

1. Uživatel jde na stránku **Platby → Pohyby**
2. Nastaví datum Od/Do a vybere formát (PDF/DOCX/XLSX)
3. Klikne **Stáhnout výpis**
4. Frontend zavolá `GET /api/v1/documents/statements/{accountId}?format=...&from=...&to=...` přes API gateway
5. Documents API zavolá Accounts API pro seznam transakcí
6. StatementGenerator vygeneruje binární soubor
7. Frontend přijme `byte[]`, zakóduje do Base64 a spustí JS download

---

## Závislosti (balíčky)

Přidány do `Directory.Packages.props`:
- `ClosedXML` v0.105.0 (XLSX generování)
- `DocumentFormat.OpenXml` v3.1.1 (DOCX generování)

---

## Testy

Přidány unit testy v `tests/FairBank.Documents.UnitTests/`:
- `GetAccountTransactionsQueryHandlerTests`
- `GenerateStatementCommandHandlerTests`
- `StatementGeneratorTests`
