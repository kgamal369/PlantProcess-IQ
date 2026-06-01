param(
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = "C:\Workspace\PlantProcess-IQ"
$psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"
$sqlPath = "$root\Backend\database\scripts\312_p03_p04_completion_pack_a.sql"

$env:PGPASSWORD = "plantprocess123"

Write-Host "Applying P03/P04 Completion Pack A SQL..." -ForegroundColor Cyan
& $psql -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -v ON_ERROR_STOP=1 -f $sqlPath

if ($LASTEXITCODE -ne 0) {
    throw "SQL apply failed."
}

$proofSql = @"
SELECT * FROM public.ppiq_p03_p04_completion_status() ORDER BY gate_code;
SELECT * FROM public.ppiq_resolve_safe_sql('DROP TABLE public.canonical_material_units;', 100, 3000);
SELECT * FROM public.ppiq_resolve_safe_sql('SELECT * FROM public.v_ppiq_p04_golden_thread', 100, 3000);
SELECT * FROM public.ppiq_validate_business_key_dictionary() ORDER BY severity, error_code LIMIT 50;
SELECT * FROM public.ppiq_validate_genealogy_graph() ORDER BY severity, error_code LIMIT 50;
SELECT * FROM public.ppiq_run_mapping_lifecycle_proof();
SELECT * FROM public.ppiq_material_investigation(
    COALESCE(
        (SELECT material_key FROM public.canonical_material_units WHERE lower(material_type) = 'coil' ORDER BY created_at_utc DESC LIMIT 1),
        (SELECT material_key FROM public.canonical_material_units ORDER BY created_at_utc DESC LIMIT 1),
        'UNKNOWN'
    )
) LIMIT 100;
"@

$tmp = Join-Path $env:TEMP "ppiq_p03p04_completion_pack_a_probe.sql"
[System.IO.File]::WriteAllText($tmp, $proofSql, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Running P03/P04 Completion Pack A DB proof..." -ForegroundColor Cyan
& $psql -h 127.0.0.1 -p 5432 -U plantprocess -d plantprocessiq -v ON_ERROR_STOP=1 -f $tmp

if ($LASTEXITCODE -ne 0) {
    throw "DB proof failed."
}

if (-not $SkipBuild) {
    Write-Host "Running backend build..." -ForegroundColor Cyan
    Push-Location "$root\Backend"
    dotnet build
    Pop-Location

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed."
    }
}

Write-Host ""
Write-Host "P03/P04 Completion Pack A validation passed." -ForegroundColor Green
