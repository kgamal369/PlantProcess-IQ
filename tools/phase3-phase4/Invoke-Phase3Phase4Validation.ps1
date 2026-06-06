[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunBuild,
    [switch]$RunFrontendBuild,
    [switch]$ApplySql,
    [string]$ConnectionString = $env:PPIQ_POSTGRES_CONNECTION
)

$ErrorActionPreference = "Stop"

function Run-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Host "---- $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

Push-Location $ProjectRoot
try {
    Run-Step "Phase 3/4 source validation" {
        node ".\tools\phase3-phase4\validate-phase3-phase4-source.cjs"
    }

    if ($ApplySql) {
        if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
            throw "ApplySql was requested, but PPIQ_POSTGRES_CONNECTION / -ConnectionString is empty. Use local Windows PostgreSQL connection explicitly."
        }

        $psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
        if (-not (Test-Path $psql)) {
            $psql = "psql"
        }

        $scripts = @(
            ".\Backend\database\scripts\310_p03_p04_mapping_genealogy_foundation.sql",
            ".\Backend\database\scripts\311_p03_p04_fix_genealogy_walk_and_safe_sql.sql",
            ".\Backend\database\scripts\312_p03_p04_completion_pack_a.sql",
            ".\Backend\database\scripts\313_p03_p04_completion_pack_a_hotfix.sql",
            ".\Backend\database\scripts\430_phase3_phase4_certification_mapping_health.sql"
        )

        foreach ($script in $scripts) {
            if (-not (Test-Path $script)) {
                throw "Missing SQL script: $script"
            }

            Run-Step "Apply $script" {
                & $psql $ConnectionString -v ON_ERROR_STOP=1 -f $script
            }
        }

        Run-Step "Phase 3/4 DB completion status" {
            & $psql $ConnectionString -v ON_ERROR_STOP=1 -c "SELECT * FROM public.ppiq_p03_p04_completion_status();"
        }

        Run-Step "Phase 3/4 mapping health status" {
            & $psql $ConnectionString -v ON_ERROR_STOP=1 -c "SELECT * FROM public.ppiq_phase34_certification_status();"
        }
    }
    else {
        Write-Host "SQL apply skipped. This is intentional unless local Windows PostgreSQL target is explicitly selected." -ForegroundColor Yellow
    }

    if ($RunBuild) {
        Push-Location ".\Backend"
        try {
            Run-Step "Backend dotnet build" {
                dotnet build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunFrontendBuild) {
        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Run-Step "Frontend npm run build" {
                npm run build
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Phase 3 + Phase 4 validation completed successfully." -ForegroundColor Green
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
}
finally {
    Pop-Location
}
