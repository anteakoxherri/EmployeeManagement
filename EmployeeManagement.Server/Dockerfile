# Konfigurimi Docker i API-se: https://aka.ms/customizecontainer shpjegon integrimin me Visual Studio.

# Baza e ekzekutimit ka vetem ASP.NET Runtime; perdoruesi APP_UID shmang ekzekutimin si root.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/uploads/profile-pictures \
    && chown -R app:app /app/uploads
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# SDK-ja perdoret per restore/build; kopjojme fillimisht projektet qe Docker te riperdore cache-in e paketave.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["EmployeeManagement.Server/EmployeeManagement.Server.csproj", "EmployeeManagement.Server/"]
COPY ["EmployeeManagement.Models/EmployeeManagement.Models.csproj", "EmployeeManagement.Models/"]
RUN dotnet restore "./EmployeeManagement.Server/EmployeeManagement.Server.csproj"
COPY EmployeeManagement.Server/ EmployeeManagement.Server/
COPY EmployeeManagement.Models/ EmployeeManagement.Models/
WORKDIR "/src/EmployeeManagement.Server"
RUN dotnet build "./EmployeeManagement.Server.csproj" -c $BUILD_CONFIGURATION --no-restore

# Publish pergatit skedaret e aplikacionit pa kerkuar SDK ne imazhin perfundimtar.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
# Publikimi per arkitekturen e kontejnerit shmang bibliotekat native per Windows/macOS.
RUN dotnet publish "./EmployeeManagement.Server.csproj" -c $BUILD_CONFIGURATION \
    --arch "$TARGETARCH" --self-contained false -o /app/publish /p:UseAppHost=false

# Bundle zbaton migrations ekzistuese si pune me vete, para nisjes se API-se.
FROM build AS migrations
ARG BUILD_CONFIGURATION=Release
RUN dotnet tool install --tool-path /tools dotnet-ef --version 8.0.33
# JWT i perkohshem sherben vetem per zbulimin e DbContext gjate build-it.
# Nuk lidhemi me databazen; sekreti real jepet vetem ne runtime nga Compose.
RUN mkdir -p /app/migrations \
    && Jwt__Key="$(cat /proc/sys/kernel/random/uuid)" \
       Jwt__Issuer=docker-build Jwt__Audience=docker-build \
       ASPNETCORE_ENVIRONMENT=Production \
       /tools/dotnet-ef migrations bundle --no-build --configuration "$BUILD_CONFIGURATION" \
       --output /app/migrations/efbundle

# Imazhi perfundimtar merr vetem rezultatin e publish; konfigurimi sekret jepet gjate ekzekutimit.
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=migrations --chmod=755 /app/migrations/efbundle ./efbundle
ENTRYPOINT ["dotnet", "EmployeeManagement.Server.dll"]
