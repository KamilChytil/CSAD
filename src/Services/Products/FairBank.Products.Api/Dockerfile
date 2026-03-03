FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/FairBank.SharedKernel/FairBank.SharedKernel.csproj src/FairBank.SharedKernel/
COPY src/Services/Products/FairBank.Products.Domain/FairBank.Products.Domain.csproj src/Services/Products/FairBank.Products.Domain/
COPY src/Services/Products/FairBank.Products.Application/FairBank.Products.Application.csproj src/Services/Products/FairBank.Products.Application/
COPY src/Services/Products/FairBank.Products.Infrastructure/FairBank.Products.Infrastructure.csproj src/Services/Products/FairBank.Products.Infrastructure/
COPY src/Services/Products/FairBank.Products.Api/FairBank.Products.Api.csproj src/Services/Products/FairBank.Products.Api/
RUN dotnet restore src/Services/Products/FairBank.Products.Api/FairBank.Products.Api.csproj

COPY src/ src/
RUN dotnet publish src/Services/Products/FairBank.Products.Api/FairBank.Products.Api.csproj -c Release -o /app/publish

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.Products.Api.dll"]
