FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/FairBank.ApiGateway/FairBank.ApiGateway.csproj src/FairBank.ApiGateway/

RUN dotnet restore src/FairBank.ApiGateway/FairBank.ApiGateway.csproj

COPY src/FairBank.ApiGateway/ src/FairBank.ApiGateway/
RUN dotnet publish src/FairBank.ApiGateway/FairBank.ApiGateway.csproj -c Release -o /app/publish --no-restore

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.ApiGateway.dll"]
