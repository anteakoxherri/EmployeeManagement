# Konfigurimi Docker i API-se: https://aka.ms/customizecontainer shpjegon integrimin me Visual Studio.

# Baza e ekzekutimit ka vetem ASP.NET Runtime; perdoruesi APP_UID shmang ekzekutimin si root.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
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
COPY . .
WORKDIR "/src/EmployeeManagement.Server"
RUN dotnet build "./EmployeeManagement.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish pergatit skedaret e aplikacionit pa kerkuar SDK ne imazhin perfundimtar.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EmployeeManagement.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Imazhi perfundimtar merr vetem rezultatin e publish; konfigurimi sekret jepet gjate ekzekutimit.
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EmployeeManagement.Server.dll"]
