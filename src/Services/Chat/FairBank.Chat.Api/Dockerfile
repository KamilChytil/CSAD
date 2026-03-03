FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY src/FairBank.SharedKernel/FairBank.SharedKernel.csproj src/FairBank.SharedKernel/
COPY src/Services/Chat/FairBank.Chat.Domain/FairBank.Chat.Domain.csproj src/Services/Chat/FairBank.Chat.Domain/
COPY src/Services/Chat/FairBank.Chat.Application/FairBank.Chat.Application.csproj src/Services/Chat/FairBank.Chat.Application/
COPY src/Services/Chat/FairBank.Chat.Infrastructure/FairBank.Chat.Infrastructure.csproj src/Services/Chat/FairBank.Chat.Infrastructure/
COPY src/Services/Chat/FairBank.Chat.Api/FairBank.Chat.Api.csproj src/Services/Chat/FairBank.Chat.Api/
RUN dotnet restore src/Services/Chat/FairBank.Chat.Api/FairBank.Chat.Api.csproj
COPY src/ src/
RUN dotnet publish src/Services/Chat/FairBank.Chat.Api/FairBank.Chat.Api.csproj -c Release -o /app/publish

FROM base AS final
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup
USER appuser:appgroup
WORKDIR /app
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FairBank.Chat.Api.dll"]
