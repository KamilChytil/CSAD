FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/FairBank.SharedKernel/FairBank.SharedKernel.csproj src/FairBank.SharedKernel/
COPY src/Services/Notifications/FairBank.Notifications.Domain/FairBank.Notifications.Domain.csproj src/Services/Notifications/FairBank.Notifications.Domain/
COPY src/Services/Notifications/FairBank.Notifications.Application/FairBank.Notifications.Application.csproj src/Services/Notifications/FairBank.Notifications.Application/
COPY src/Services/Notifications/FairBank.Notifications.Infrastructure/FairBank.Notifications.Infrastructure.csproj src/Services/Notifications/FairBank.Notifications.Infrastructure/
COPY src/Services/Notifications/FairBank.Notifications.Api/FairBank.Notifications.Api.csproj src/Services/Notifications/FairBank.Notifications.Api/
RUN dotnet restore src/Services/Notifications/FairBank.Notifications.Api/FairBank.Notifications.Api.csproj
COPY src/ src/
RUN dotnet publish src/Services/Notifications/FairBank.Notifications.Api/FairBank.Notifications.Api.csproj -c Release -o /app/publish

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.Notifications.Api.dll"]
