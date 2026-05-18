param(
    [switch]$SkipE2E
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root = "C:\Workspace\PlantProcess-IQ"
$BackendRoot = Join-Path $Root "Backend"
$FrontendRoot = Join-Path $Root "Frontend\PlantProcess.Web"

function Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "================================================================================" -ForegroundColor DarkGray
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor DarkGray
}

Step "Backend restore"
Push-Location $BackendRoot
dotnet restore .\PlantProcessIQ.sln

Step "Backend build"
dotnet build .\PlantProcessIQ.sln --no-restore

Step "Backend tests"
dotnet test .\PlantProcessIQ.sln --no-build
Pop-Location

Step "Frontend install"
Push-Location $FrontendRoot
npm install

Step "Frontend build"
npm run build

Step "Frontend unit/integration tests"
npm run test

Step "Frontend lint"
npm run lint

if (-not $SkipE2E) {
    Step "Full-stack E2E tests"
    npm run e2e
}
else {
    Write-Host "Skipping E2E tests." -ForegroundColor Yellow
}

Pop-Location

Step "Full validation completed"
