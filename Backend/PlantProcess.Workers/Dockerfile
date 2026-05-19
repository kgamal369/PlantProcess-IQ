# ============================================================
# PlantProcess IQ - Workers Dockerfile
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY PlantProcessIQ.sln ./
COPY PlantProcess.Domain/PlantProcess.Domain.csproj PlantProcess.Domain/
COPY PlantProcess.Application/PlantProcess.Application.csproj PlantProcess.Application/
COPY PlantProcess.Infrastructure/PlantProcess.Infrastructure.csproj PlantProcess.Infrastructure/
COPY PlantProcess.Workers/PlantProcess.Workers.csproj PlantProcess.Workers/

RUN dotnet restore PlantProcess.Workers/PlantProcess.Workers.csproj

COPY . .

RUN dotnet publish PlantProcess.Workers/PlantProcess.Workers.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PlantProcess.Workers.dll"]