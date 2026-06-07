$ErrorActionPreference = "Stop"
cd C:\Workspace\PlantProcess-IQ\Backend

$env:ASPNETCORE_ENVIRONMENT = [Environment]::GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "User")
$env:DOTNET_ENVIRONMENT = [Environment]::GetEnvironmentVariable("DOTNET_ENVIRONMENT", "User")
$env:ASPNETCORE_URLS = [Environment]::GetEnvironmentVariable("ASPNETCORE_URLS", "User")
$env:ConnectionStrings__PlantProcessDb = [Environment]::GetEnvironmentVariable("ConnectionStrings__PlantProcessDb", "User")
$env:PLANTPROCESS_ALLOWED_ORIGINS = [Environment]::GetEnvironmentVariable("PLANTPROCESS_ALLOWED_ORIGINS", "User")

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__PlantProcessDb)) {
    throw "Missing ConnectionStrings__PlantProcessDb user environment variable."
}

Write-Host "Starting PlantProcess IQ API on $env:ASPNETCORE_URLS" -ForegroundColor Cyan
dotnet run --no-launch-profile --project .\PlantProcess.Api\PlantProcess.Api.csproj
