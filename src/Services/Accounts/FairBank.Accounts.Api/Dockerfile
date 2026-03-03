FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .

COPY src/FairBank.SharedKernel/FairBank.SharedKernel.csproj src/FairBank.SharedKernel/
COPY src/Services/Accounts/FairBank.Accounts.Domain/FairBank.Accounts.Domain.csproj src/Services/Accounts/FairBank.Accounts.Domain/
COPY src/Services/Accounts/FairBank.Accounts.Application/FairBank.Accounts.Application.csproj src/Services/Accounts/FairBank.Accounts.Application/
COPY src/Services/Accounts/FairBank.Accounts.Infrastructure/FairBank.Accounts.Infrastructure.csproj src/Services/Accounts/FairBank.Accounts.Infrastructure/
COPY src/Services/Accounts/FairBank.Accounts.Api/FairBank.Accounts.Api.csproj src/Services/Accounts/FairBank.Accounts.Api/

RUN dotnet restore src/Services/Accounts/FairBank.Accounts.Api/FairBank.Accounts.Api.csproj

COPY src/ src/
RUN dotnet publish src/Services/Accounts/FairBank.Accounts.Api/FairBank.Accounts.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.Accounts.Api.dll"]
