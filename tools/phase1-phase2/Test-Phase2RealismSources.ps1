[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

$infra = Join-Path $ProjectRoot "Infrastructure\demo-sources"

$required = @(
    "meltshop-postgres\init\001_schema_seed.sql",
    "caster-oracle\init\001_schema_seed.sql",
    "hsm-oracle\init\001_schema_seed.sql",
    "pkl-mssql\init\001_schema_seed.sql",
    "parsytec-mysql\init\001_schema_seed.sql",
    "downtime-mysql\init\001_schema_seed.sql",
    "excel-yard\yard_inventory.csv",
    "excel-qa\qa_samples.csv"
)

foreach ($r in $required) {
    $p = Join-Path $infra $r
    if (-not (Test-Path $p)) {
        throw "Missing Phase 2 source file: $r"
    }
}

$yard = Import-Csv (Join-Path $infra "excel-yard\yard_inventory.csv")
$qa = Import-Csv (Join-Path $infra "excel-qa\qa_samples.csv")

if ($yard.Count -lt 5400 -or $yard.Count -gt 5805) {
    throw "Yard coil count outside realism band. Actual: $($yard.Count)"
}

if ($qa.Count -lt 5400 -or $qa.Count -gt 5805) {
    throw "QA sample count outside realism band. Actual: $($qa.Count)"
}

$ref = $yard | Where-Object { $_.coil_id -eq "C-0044170" } | Select-Object -First 1
if (-not $ref) { throw "C-0044170 missing from yard source." }
if ($ref.bay -ne "BAY-03") { throw "C-0044170 yard bay trim failed. Actual: '$($ref.bay)'" }

$qaRef = $qa | Where-Object { $_.coil_id -eq "C-0044170" } | Select-Object -First 1
if (-not $qaRef) { throw "C-0044170 missing from QA source." }
if ($qaRef.heat_id -ne "H-3361") { throw "C-0044170 QA heat link expected H-3361, actual $($qaRef.heat_id)" }
if ($qaRef.defect_code -ne "EDGE_CRACK") { throw "C-0044170 expected EDGE_CRACK." }

$missingLabels = $qa | Where-Object { [string]::IsNullOrWhiteSpace($_.quality_label) }
if ($missingLabels.Count -lt 1) {
    throw "Deliberate imperfection missing: expected blank quality labels."
}

$allText = ""
foreach ($r in $required) {
    $allText += [IO.File]::ReadAllText((Join-Path $infra $r)) + "`n"
}

foreach ($needle in @("C-0044170","H-3361","S-0044170","EDGE_CRACK","UNMAPPED_DEMO_DEFECT","C-ORPHAN-0001")) {
    if ($allText -notmatch [Regex]::Escape($needle)) {
        throw "Missing required realism fixture token: $needle"
    }
}

Write-Host "Phase 2 realism source validation passed." -ForegroundColor Green
Write-Host "Yard rows: $($yard.Count)"
Write-Host "QA rows  : $($qa.Count)"
Write-Host "Missing-label imperfection rows: $($missingLabels.Count)"