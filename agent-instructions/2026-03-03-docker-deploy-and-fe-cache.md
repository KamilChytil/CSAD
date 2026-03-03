# 2026-03-03 — Docker Deploy, Bug Fixes & FE Cache + Motto

## Souhrn změn

Deploynuto Docker prostředí, opraveny runtime chyby, přidán nastavitelný FE caching a změněno motto.

**9 změněných souborů, +132 / −8 řádků**

---

## 1. Docker — opravy runtime chyb

### PostgreSQL Replica — spouštění jako root (`replica-entrypoint.sh`)
- **Problém:** `exec postgres` probíhal jako root → PostgreSQL odmítl start z bezpečnostních důvodů
- **Fix:** Přidán `chown -R postgres:postgres` + `chmod 0700` na data directory, start přes `su-exec postgres`

### Web App Health Check — IPv6 problém (`Dockerfile`)
- **Problém:** `wget http://localhost:80/` se resolvoval na `[::1]` (IPv6), nginx naslouchal pouze na IPv4
- **Fix:** Změněno na `http://127.0.0.1:80/`

---

## 2. Nastavitelný FE caching

### Nové soubory
- `src/FairBank.Web/nginx.conf.template` — šablona nginx konfigurace s placeholder bloky `__CACHE_BLOCK_START__` / `__CACHE_BLOCK_END__`
- `src/FairBank.Web/docker-entrypoint.sh` — entrypoint skript, generuje finální nginx config na základě env `ENABLE_CACHE`

### Upravené soubory
- `src/FairBank.Web/Dockerfile` — používá nový entrypoint + template místo statického `nginx.conf`
- `docker-compose.yml` — přidána env `ENABLE_CACHE: "true"` do service `web-app`

### Použití
```yaml
# docker-compose.yml — web-app service
environment:
  ENABLE_CACHE: "true"    # "true" = produkční caching, "false" = žádný cache (testování)
```

Při `ENABLE_CACHE=true`:
- `.dll/.wasm/.dat/.blat` → `expires 7d`, `Cache-Control: public, immutable`
- `.js/.css/.png/...` → `expires 1d`, `Cache-Control: public, must-revalidate`

Při `ENABLE_CACHE=false`:
- Všechny odpovědi: `Cache-Control: no-store, no-cache`, `Pragma: no-cache`, `expires -1`

---

## 3. Změna motta

**Staré:** „Tvůj život, tvé finance, tvá hra."
**Nové:** „Jediná banka na kterou můžete vsadit."

Změněno ve 4 souborech:
- `src/FairBank.Web/wwwroot/index.html` — `<title>`
- `src/FairBank.Web/Layout/SideNav.razor` — tagline v navigaci
- `src/FairBank.Web.Auth/Pages/Login.razor` — subtitle na login stránce
- `src/FairBank.Web.Shared/wwwroot/css/vabank-theme.css` — CSS komentář

---

## Diff

```diff
 docker-compose.yml                                 |  2 +
 docker/postgres/replica-entrypoint.sh              |  8 ++-
 src/FairBank.Web.Auth/Pages/Login.razor            |  2 +-
 .../wwwroot/css/vabank-theme.css                   |  2 +-
 src/FairBank.Web/Dockerfile                        | 11 +++-
 src/FairBank.Web/Layout/SideNav.razor              |  2 +-
 src/FairBank.Web/docker-entrypoint.sh              | 53 ++++++++++++++++++++ (new)
 src/FairBank.Web/nginx.conf.template               | 58 ++++++++++++++++++++ (new)
 src/FairBank.Web/wwwroot/index.html                |  2 +-
 9 files changed, 132 insertions(+), 8 deletions(-)
```

### docker-compose.yml
```diff
@@ -8,6 +8,8 @@ services:
     container_name: fairbank-web
     ports:
       - "80:80"
+    environment:
+      ENABLE_CACHE: "true"    # set to "false" to disable FE caching (for testing)
     depends_on:
       - api-gateway
```

### docker/postgres/replica-entrypoint.sh
```diff
@@ -22,7 +22,11 @@
   echo "Base backup complete. Starting replica..."
 fi

-# Start PostgreSQL
-exec postgres \
+# Ensure correct ownership and permissions
+chown -R postgres:postgres /var/lib/postgresql/data
+chmod 0700 /var/lib/postgresql/data
+
+# Start PostgreSQL as postgres user
+exec su-exec postgres postgres \
   -c hot_standby=on \
   -c shared_buffers=64MB
```

### src/FairBank.Web/Dockerfile
```diff
@@ -35,9 +35,16 @@ FROM nginx:alpine AS final
 RUN rm /etc/nginx/conf.d/default.conf

 COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
-COPY src/FairBank.Web/nginx.conf /etc/nginx/conf.d/default.conf
+COPY src/FairBank.Web/nginx.conf.template /etc/nginx/conf.d/default.conf.template
+COPY src/FairBank.Web/docker-entrypoint.sh /docker-entrypoint-vabank.sh
+RUN chmod +x /docker-entrypoint-vabank.sh
+
+# Default: caching ON (set ENABLE_CACHE=false to disable)
+ENV ENABLE_CACHE=true

 EXPOSE 80

 HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
-    CMD wget --no-verbose --tries=1 --spider http://localhost:80/ || exit 1
+    CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:80/ || exit 1
+
+ENTRYPOINT ["/docker-entrypoint-vabank.sh"]
```

### src/FairBank.Web.Auth/Pages/Login.razor
```diff
@@ -12,7 +12,7 @@
         <h1 class="auth-title">VA-BANK</h1>
-        <p class="auth-subtitle">Tvůj život, tvé finance, tvá hra.</p>
+        <p class="auth-subtitle">Jediná banka na kterou můžete vsadit.</p>
```

### src/FairBank.Web/Layout/SideNav.razor
```diff
@@ -46,7 +46,7 @@
     <div class="side-nav-footer">
-        <span class="side-nav-tagline">Tvůj život, tvé finance, tvá hra.</span>
+        <span class="side-nav-tagline">Jediná banka na kterou můžete vsadit.</span>
     </div>
```

### src/FairBank.Web/wwwroot/index.html
```diff
@@ -5,7 +5,7 @@
     <meta name="theme-color" content="#1A1A1A" />
-    <title>Va-bank – Tvůj život, tvé finance, tvá hra</title>
+    <title>Va-bank – jediná banka na kterou můžete vsadit</title>
```

### src/FairBank.Web.Shared/wwwroot/css/vabank-theme.css
```diff
@@ -1,6 +1,6 @@
 /* ═══════════════════════════════════════════════════════════
    VA-BANK THEME — Casino Banking UI
-   Tvůj život, tvé finance, tvá hra.
+   Jediná banka na kterou můžete vsadit.
    ═══════════════════════════════════════════════════════════ */
```

### src/FairBank.Web/docker-entrypoint.sh (nový soubor)
Entrypoint skript: čte `nginx.conf.template`, nahradí cache placeholder blok podle `ENABLE_CACHE` env, spustí nginx.

### src/FairBank.Web/nginx.conf.template (nový soubor)
Kopie původního `nginx.conf` s cache sekcí nahrazenou placeholder markery `__CACHE_BLOCK_START__` / `__CACHE_BLOCK_END__`.
