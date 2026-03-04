FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/FairBank.SharedKernel/FairBank.SharedKernel.csproj src/FairBank.SharedKernel/
COPY src/Services/Documents/FairBank.Documents.Domain/FairBank.Documents.Domain.csproj src/Services/Documents/FairBank.Documents.Domain/
COPY src/Services/Documents/FairBank.Documents.Application/FairBank.Documents.Application.csproj src/Services/Documents/FairBank.Documents.Application/
COPY src/Services/Documents/FairBank.Documents.Infrastructure/FairBank.Documents.Infrastructure.csproj src/Services/Documents/FairBank.Documents.Infrastructure/
COPY src/Services/Documents/FairBank.Documents.Api/FairBank.Documents.Api.csproj src/Services/Documents/FairBank.Documents.Api/
RUN dotnet restore src/Services/Documents/FairBank.Documents.Api/FairBank.Documents.Api.csproj
COPY src/FairBank.SharedKernel/ src/FairBank.SharedKernel/
COPY src/Services/Documents/ src/Services/Documents/
RUN dotnet publish src/Services/Documents/FairBank.Documents.Api/FairBank.Documents.Api.csproj -c Release -o /app/publish

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.Documents.Api.dll"]
