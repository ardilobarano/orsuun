# Build from the repo root:  docker build -f deploy/Dockerfile -t orsuun-server .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Orsuun.Rules/Orsuun.Rules.csproj src/Orsuun.Rules/
COPY src/Orsuun.Server/Orsuun.Server.csproj src/Orsuun.Server/
RUN dotnet restore src/Orsuun.Server/Orsuun.Server.csproj
COPY src/ src/
RUN dotnet publish src/Orsuun.Server/Orsuun.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
# Time zone data for Evening Bells (Europe/Istanbul).
RUN apt-get update && apt-get install -y --no-install-recommends tzdata curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --retries=5 CMD curl -fs http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Orsuun.Server.dll"]
