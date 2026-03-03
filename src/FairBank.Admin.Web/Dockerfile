# FairBank Admin Web App
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/FairBank.Admin.Web/FairBank.Admin.Web.csproj src/FairBank.Admin.Web/
RUN dotnet restore src/FairBank.Admin.Web/FairBank.Admin.Web.csproj
COPY src/ src/
RUN dotnet publish src/FairBank.Admin.Web/FairBank.Admin.Web.csproj -c Release -o /app/publish

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
WORKDIR /app
COPY --from=build /app/publish .
RUN chown -R appuser:appgroup /app
USER appuser:appgroup
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/ || exit 1
ENTRYPOINT ["dotnet", "FairBank.Admin.Web.dll"]
