# ============================================================
# PlantProcess IQ - API Dockerfile
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY PlantProcessIQ.sln ./
COPY PlantProcess.Domain/PlantProcess.Domain.csproj PlantProcess.Domain/
COPY PlantProcess.Application/PlantProcess.Application.csproj PlantProcess.Application/
COPY PlantProcess.Infrastructure/PlantProcess.Infrastructure.csproj PlantProcess.Infrastructure/
COPY PlantProcess.Api/PlantProcess.Api.csproj PlantProcess.Api/

RUN dotnet restore PlantProcessIQ.sln

COPY . .

RUN dotnet publish PlantProcess.Api/PlantProcess.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:5063
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 5063

ENTRYPOINT ["dotnet", "PlantProcess.Api.dll"]