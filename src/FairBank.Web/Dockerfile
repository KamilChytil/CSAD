# ── Build stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .

# Restore (copy only csproj files for layer caching)
COPY src/FairBank.Web.Shared/FairBank.Web.Shared.csproj       src/FairBank.Web.Shared/
COPY src/FairBank.Web.Auth/FairBank.Web.Auth.csproj          src/FairBank.Web.Auth/
COPY src/FairBank.Web.Overview/FairBank.Web.Overview.csproj    src/FairBank.Web.Overview/
COPY src/FairBank.Web.Payments/FairBank.Web.Payments.csproj    src/FairBank.Web.Payments/
COPY src/FairBank.Web.Savings/FairBank.Web.Savings.csproj      src/FairBank.Web.Savings/
COPY src/FairBank.Web.Investments/FairBank.Web.Investments.csproj src/FairBank.Web.Investments/
COPY src/FairBank.Web.Profile/FairBank.Web.Profile.csproj      src/FairBank.Web.Profile/
COPY src/FairBank.Web.Exchange/FairBank.Web.Exchange.csproj    src/FairBank.Web.Exchange/
COPY src/FairBank.Admin.Web/FairBank.Admin.Web.csproj          src/FairBank.Admin.Web/
COPY src/FairBank.Web/FairBank.Web.csproj                      src/FairBank.Web/
RUN dotnet restore src/FairBank.Web/FairBank.Web.csproj

# Build & publish
COPY src/FairBank.Web.Shared/      src/FairBank.Web.Shared/
COPY src/FairBank.Web.Auth/        src/FairBank.Web.Auth/
COPY src/FairBank.Web.Overview/    src/FairBank.Web.Overview/
COPY src/FairBank.Web.Payments/    src/FairBank.Web.Payments/
COPY src/FairBank.Web.Savings/     src/FairBank.Web.Savings/
COPY src/FairBank.Web.Investments/ src/FairBank.Web.Investments/
COPY src/FairBank.Web.Profile/     src/FairBank.Web.Profile/
COPY src/FairBank.Web.Exchange/    src/FairBank.Web.Exchange/
COPY src/FairBank.Admin.Web/       src/FairBank.Admin.Web/
COPY src/FairBank.Web/             src/FairBank.Web/
RUN dotnet publish src/FairBank.Web/FairBank.Web.csproj -c Release -o /app/publish

# ── Runtime stage — nginx serves static WASM + proxies API ──
FROM nginx:alpine AS final

RUN rm /etc/nginx/conf.d/default.conf

COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/FairBank.Web/nginx.conf.template /etc/nginx/conf.d/default.conf.template
COPY src/FairBank.Web/docker-entrypoint.sh /docker-entrypoint-vabank.sh
RUN sed -i 's/\r//' /docker-entrypoint-vabank.sh && chmod +x /docker-entrypoint-vabank.sh

# Default: caching ON (set ENABLE_CACHE=false to disable)
ENV ENABLE_CACHE=true

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:80/ || exit 1

ENTRYPOINT ["/docker-entrypoint-vabank.sh"]
