FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .

COPY src/FairBank.SharedKernel/FairBank.SharedKernel.csproj src/FairBank.SharedKernel/
COPY src/Services/Identity/FairBank.Identity.Domain/FairBank.Identity.Domain.csproj src/Services/Identity/FairBank.Identity.Domain/
COPY src/Services/Identity/FairBank.Identity.Application/FairBank.Identity.Application.csproj src/Services/Identity/FairBank.Identity.Application/
COPY src/Services/Identity/FairBank.Identity.Infrastructure/FairBank.Identity.Infrastructure.csproj src/Services/Identity/FairBank.Identity.Infrastructure/
COPY src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj src/Services/Identity/FairBank.Identity.Api/

RUN dotnet restore src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj

COPY src/ src/
RUN dotnet publish src/Services/Identity/FairBank.Identity.Api/FairBank.Identity.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.Identity.Api.dll"]
