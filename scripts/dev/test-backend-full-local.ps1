$ErrorActionPreference = "Stop"

$Root = "C:\Workspace\PlantProcess-IQ"
$Backend = Join-Path $Root "Backend"

# Local Windows PostgreSQL test baseline.
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:ConnectionStrings__PlantProcessDb = "Host=127.0.0.1;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123"
$env:PPIQ_TEST_CONNECTION_STRING = $env:ConnectionStrings__PlantProcessDb
$env:PLANTPROCESS_ALLOWED_ORIGINS = "http://localhost:5173,http://localhost:3000"

# Full dotnet test uses an external test API host on 15063.
# Do not use the dev API on 5063 for full test-suite execution.
$env:PPIQ_FORCE_EXTERNAL_API_TEST_HOST = "1"
$env:PPIQ_TEST_API_PORT = "15063"
$env:PPIQ_RUN_CONNECTOR_INTEGRATION = "0"

Write-Host ""
Write-Host "=================================================================================================" -ForegroundColor DarkCyan
Write-Host "PlantProcess IQ FULL backend dotnet test - local mode" -ForegroundColor Cyan
Write-Host "=================================================================================================" -ForegroundColor DarkCyan
Write-Host "Backend        : $Backend"
Write-Host "Test API port  : 15063"
Write-Host "DB             : 127.0.0.1:5432/plantprocessiq"
Write-Host "External host  : $env:PPIQ_FORCE_EXTERNAL_API_TEST_HOST"
Write-Host ""

Get-Process testhost -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process vstest.console -ErrorAction SilentlyContinue | Stop-Process -Force

Get-NetTCPConnection -LocalPort 15063 -State Listen -ErrorAction SilentlyContinue |
  Select-Object -ExpandProperty OwningProcess -Unique |
  ForEach-Object {
      Write-Host "Stopping old PPIQ external test API PID=$_" -ForegroundColor Yellow
      Stop-Process -Id $_ -Force
  }

Start-Sleep -Seconds 2

Push-Location $Backend
try {
    dotnet test
}
finally {
    Pop-Location
}