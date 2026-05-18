param(
    [switch]$SkipFrontend,
    [switch]$SkipBackend
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root = "C:\Workspace\PlantProcess-IQ"
$BackendRoot = Join-Path $Root "Backend"
$FrontendRoot = Join-Path $Root "Frontend\PlantProcess.Web"

function Step($msg) {
    Write-Host ""
    Write-Host "================================================================================" -ForegroundColor DarkGray
    Write-Host $msg -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor DarkGray
}

if (-not $SkipBackend) {
    Step "Backend restore/build/test"
    Push-Location $BackendRoot
    dotnet restore .\PlantProcessIQ.sln
    dotnet build .\PlantProcessIQ.sln --no-restore
    dotnet test .\PlantProcessIQ.sln --no-build
    Pop-Location
}

if (-not $SkipFrontend) {
    Step "Frontend install/build/lint"
    Push-Location $FrontendRoot
    npm install
    npm run build
    npm run lint
    Pop-Location
}

Step "Validation completed"
