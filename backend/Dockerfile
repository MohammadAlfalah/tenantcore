# ---- Build stage ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached unless the project file changes).
COPY src/TenantCore.Api/TenantCore.Api.csproj src/TenantCore.Api/
RUN dotnet restore src/TenantCore.Api/TenantCore.Api.csproj

# Then copy the rest and publish.
COPY src/ src/
RUN dotnet publish src/TenantCore.Api/TenantCore.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage --------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "TenantCore.Api.dll"]
