FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY PlantProcessIQ.sln ./
COPY PlantProcess.Api/PlantProcess.Api.csproj PlantProcess.Api/
COPY PlantProcess.Application/PlantProcess.Application.csproj PlantProcess.Application/
COPY PlantProcess.Domain/PlantProcess.Domain.csproj PlantProcess.Domain/
COPY PlantProcess.Infrastructure/PlantProcess.Infrastructure.csproj PlantProcess.Infrastructure/
COPY PlantProcess.Workers/PlantProcess.Workers.csproj PlantProcess.Workers/

RUN dotnet restore PlantProcess.Api/PlantProcess.Api.csproj

COPY . .

RUN dotnet publish PlantProcess.Api/PlantProcess.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "PlantProcess.Api.dll"]