$ErrorActionPreference = "Stop"

$Root = "C:\Workspace\PlantProcess-IQ"
$Backend = Join-Path $Root "Backend"

# Local Windows PostgreSQL / local API mode.
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5063"
$env:ConnectionStrings__PlantProcessDb = "Host=localhost;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only"
$env:PLANTPROCESS_ALLOWED_ORIGINS = "http://localhost:5173,http://localhost:3000"

# Force API integration tests to use the real local API host instead of unstable WebApplicationFactory/TestServer.
$env:PPIQ_FORCE_EXTERNAL_API_TEST_HOST = "1"
$env:PPIQ_TEST_API_PORT = "5063"
$env:PPIQ_TEST_CONNECTION_STRING = $env:ConnectionStrings__PlantProcessDb

# Keep heavy opt-in connector integrations disabled unless explicitly enabled.
$env:PPIQ_RUN_CONNECTOR_INTEGRATION = "0"

Write-Host ""
Write-Host "=================================================================================================" -ForegroundColor DarkCyan
Write-Host "PlantProcess IQ local backend test mode" -ForegroundColor Cyan
Write-Host "=================================================================================================" -ForegroundColor DarkCyan
Write-Host "Backend       : $Backend"
Write-Host "API expected  : http://localhost:5063"
Write-Host "DB            : 127.0.0.1:5432/plantprocessiq"
Write-Host "External host : $env:PPIQ_FORCE_EXTERNAL_API_TEST_HOST"
Write-Host ""

try {
    $health = Invoke-WebRequest -Uri "http://localhost:5063/health" -UseBasicParsing -TimeoutSec 5
    Write-Host "API health reachable: $($health.StatusCode)" -ForegroundColor Green
}
catch {
    Write-Host "API is not reachable on http://localhost:5063/health" -ForegroundColor Red
    Write-Host "Start it in another PowerShell first:" -ForegroundColor Yellow
    Write-Host "cd C:\Workspace\PlantProcess-IQ"
    Write-Host ".\scripts\dev\run-api-local.ps1"
    throw
}

Push-Location $Root
try {
    Write-Host ""
    Write-Host "---- Backend build" -ForegroundColor Cyan
    dotnet build .\Backend --no-incremental /m:1

    Write-Host ""
    Write-Host "---- T-047 targeted tests" -ForegroundColor Cyan
    dotnet test .\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj `
      --filter "FullyQualifiedName~Phase9_T047SuggestionWorkflowCertificationTests" `
      --no-build

    Write-Host ""
    Write-Host "---- API integration smoke only, external local API mode" -ForegroundColor Cyan
    dotnet test .\Backend\tests\PlantProcess.Api.IntegrationTests\PlantProcess.Api.IntegrationTests.csproj `
      --filter "FullyQualifiedName~Smoke|FullyQualifiedName~OpenApi|FullyQualifiedName~Health" `
      --no-build

    Write-Host ""
    Write-Host "PPIQ local backend validation completed." -ForegroundColor Green
}
finally {
    Pop-Location
}