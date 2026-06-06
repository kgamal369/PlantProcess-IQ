param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunBuild,
    [switch]$RunFrontendBuild,
    [switch]$ApplySql,
    [string]$PostgresConnectionString = $env:PPIQ_POSTGRES_CONNECTION
)

$ErrorActionPreference = "Stop"

function Run-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Host "---- $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}

function Assert-FileContains([string]$Path, [string]$Needle) {
    if (-not (Test-Path $Path)) { throw "Missing required file: $Path" }
    $content = [System.IO.File]::ReadAllText($Path)
    if ($content -notlike "*$Needle*") { throw "File $Path does not contain required marker: $Needle" }
}

Push-Location $ProjectRoot
try {
    Run-Step "Phase 1/2 BOM gate" {
        powershell -ExecutionPolicy Bypass -File .\tools\phase1-phase2\Test-Utf8NoBom.ps1 -ProjectRoot $ProjectRoot
    }

    Run-Step "Phase 1/2 production-runtime secret scan" {
        powershell -ExecutionPolicy Bypass -File .\tools\phase1-phase2\Invoke-SecretScan.ps1 -ProjectRoot $ProjectRoot
    }

    Run-Step "Phase 3/4 source markers" {
        Assert-FileContains ".\Backend\tests\PlantProcess.Application.UnitTests\Phase3Phase4\Phase3Phase4CertificationTests.cs" "Phase3Phase4CertificationTests"
        Assert-FileContains ".\Backend\database\scripts\430_phase3_phase4_certification_mapping_health.sql" "ppiq_detect_schema_drift"
        Assert-FileContains ".\Backend\PlantProcess.Api\Endpoints\MappingHealth\Phase34MappingHealthEndpoints.cs" "MapPhase34MappingHealthEndpoints"
        Assert-FileContains ".\Backend\PlantProcess.Api\Program.cs" "MapPhase34MappingHealthEndpoints"
        Assert-FileContains ".\Frontend\PlantProcess.Web\src\pages\MappingHealth\MappingHealthPage.tsx" "MappingHealthPage"
        Assert-FileContains ".\Frontend\PlantProcess.Web\src\App.implementation.tsx" "MappingHealthPage"
        Assert-FileContains ".\Frontend\PlantProcess.Web\src\App.implementation.tsx" 'path="/mapping-health"'
        Write-Host "Phase 3/4 source markers passed." -ForegroundColor Green
    }

    if ($ApplySql) {
        if ([string]::IsNullOrWhiteSpace($PostgresConnectionString)) { throw "ApplySql was requested but PPIQ_POSTGRES_CONNECTION/PostgresConnectionString is empty." }
        $psql = Get-Command psql -ErrorAction SilentlyContinue
        if (-not $psql) { throw "ApplySql was requested but psql was not found in PATH." }
        Run-Step "Apply Phase 3/4 SQL" {
            psql $PostgresConnectionString -v ON_ERROR_STOP=1 -f ".\Backend\database\scripts\430_phase3_phase4_certification_mapping_health.sql"
        }
        Run-Step "Verify Phase 3/4 SQL certification function" {
            psql $PostgresConnectionString -v ON_ERROR_STOP=1 -c "SELECT * FROM public.ppiq_phase34_certification_status();"
        }
    } else {
        Write-Host ""
        Write-Host "SQL apply skipped. This is intentional until local/server DB target is selected." -ForegroundColor DarkYellow
    }

    if ($RunBuild) {
        Push-Location ".\Backend"
        try {
            Run-Step "dotnet restore" { dotnet restore }
            Run-Step "dotnet build" { dotnet build --no-restore }
            Run-Step "dotnet test Phase3Phase4CertificationTests" { dotnet test ".\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj" --no-build --filter "FullyQualifiedName~Phase3Phase4CertificationTests" }
        } finally { Pop-Location }
    }

    if ($RunFrontendBuild) {
        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Run-Step "npm install/check" { if (Test-Path "package-lock.json") { npm ci } else { npm install } }
            Run-Step "npm run build" { npm run build }
        } finally { Pop-Location }
    }

    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Phase 3 + Phase 4 validation completed successfully." -ForegroundColor Green
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
} finally {
    Pop-Location
}
