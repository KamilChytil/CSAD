# Poslední změny - Admin Portál a Docker Fixes

> **Datum:** 2026-03-03
> **Autor:** GitHub Copilot
> **Status:** Dokončeno

Tento dokument shrnuje nejnovější změny v projektu, které zahrnují opravu Docker buildu a migraci Admin portálu.

## 1. Oprava Docker Buildu

Byla opravena kritická chyba v `src/FairBank.Web/Dockerfile`, která bránila sestavení celého stacku.

- **Problém:** Dockerfile odkazoval na neexistující složku `src/FairBank.Web.Admin/`.
- **Řešení:** Cesty byly aktualizovány na správný název projektu `src/FairBank.Admin.Web/`.
- **Dopad:** `docker compose build` je nyní plně funkční.

## 2. Migrace Admin Portálu na Blazor Server

Projekt Admin portálu byl kompletně přepracován z Client-side WASM na server-side Blazor (Blazor Server) za účelem přímého přístupu k logům a integrace s Kafkou.

- **Nové umístění:** `src/FairBank.Admin.Web/`
- **Nové funkce:**
  - **Kafka Consumer:** Implementován `KafkaLogConsumerService`, který v reálném čase odebírá logy z topiku `system-logs`.
  - **Log Persistence:** Přidáno SQLite úložiště (`LogDbContext`) pro trvalé ukládání logů.
  - **UI Dashboard:** Nová komponenta `Admin.razor` zobrazující posledních 50 logů přímo z databáze.
  - **API Endpoints:** Přidány endpointy `/api/logs` pro programový přístup k logům (OpenAPI/Scalar dokumentace).

## 3. Infrastrukturní úpravy

- **Odstranění sirotků:** Byl smazán původní nefunkční projekt `src/FairBank.Web.Admin/`.
- **Clean-up:** Hlavní webová aplikace `FairBank.Web` byla očištěna od referencí na starý admin projekt (v `.csproj` i `App.razor`).
- **Docker Network Clean:** Byly vyřešeny problémy se "stuck" endpointy v síti `csad_backend` vynuceným pročištěním kontejnerů.

## 4. Aktuální stav služeb

| Služba | Technologie | Stav |
|--|--|--|
| `fairbank-web` | Blazor WASM | Funkční |
| `fairbank-admin-web` | Blazor Server | Funkční (Kafka enabled) |
| `fairbank-api-gateway` | YARP | Funkční |
| `fairbank-pg-primary` | PostgreSQL | Funkční |
| `fairbank-kafka` | Kafka | Funkční |

---

## Příkazy pro spuštění

```powershell
# Kompletní sestavení a spuštění
docker compose down
docker compose build
docker compose up -d
```
