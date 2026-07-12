#requires -Version 5.1
<#
  Diag-CanonicalReadPath.ps1
  --------------------------
  READ-ONLY. Answers the Stage-C question: are canonical_material_units and
  canonical_genealogy_edges live as VIEWS (over material_units / genealogy_edges,
  per script 310) or as DRIFTED TABLES (stale, fed by the Arch-B ladder)?
  Also reports base-vs-canonical row counts and the count of imported heats.

  No writes. Self-locates psql.exe and connects to the native ppiq_app Postgres.

  Launch (immune to execution policy / mark-of-the-web):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Diag-CanonicalReadPath.ps1
#>

[CmdletBinding()]
param(
    [string]$DbHost = '127.0.0.1',
    [int]   $Port   = 5432,
    [string]$Database = 'ppiq_app',
    [string]$User   = 'ppiq_dev',
    [string]$Pass   = 'ppiq_dev_local_only'
)

$ErrorActionPreference = 'Stop'
function Section($t){ Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Info($t){ Write-Host "     $t" -ForegroundColor Gray }
function Bad($t){ Write-Host "FAIL: $t" -ForegroundColor Red }

# --- locate psql.exe ---
$psql = (Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) {
    $cand = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1
    if ($cand) { $psql = $cand.FullName }
}
if (-not $psql) { Bad "psql.exe not found on PATH or under C:\Program Files\PostgreSQL\*\bin. Tell me your PostgreSQL bin path."; exit 1 }
Info "using psql: $psql"

$env:PGPASSWORD = $Pass

function Run-Sql([string]$label,[string]$sql){
    Section $label
    $out = & $psql -h $DbHost -p $Port -d $Database -U $User -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { Bad "query failed:"; $out | ForEach-Object { Write-Host "     $_" -ForegroundColor DarkYellow }; return }
    $out | ForEach-Object { Write-Host "     $_" }
}

Run-Sql "1. Object kind (v = VIEW / good, r = TABLE / drifted)" @"
SELECT relname,
       CASE relkind WHEN 'v' THEN 'VIEW' WHEN 'r' THEN 'TABLE' WHEN 'm' THEN 'MATVIEW' ELSE relkind::text END AS kind
FROM pg_class
WHERE relname IN ('canonical_material_units','canonical_genealogy_edges','material_units','genealogy_edges')
ORDER BY relname;
"@

Run-Sql "2. Base vs canonical row counts (should match if canonical is a view)" @"
SELECT 'material_units'            AS object, count(*) AS n FROM material_units
UNION ALL
SELECT 'canonical_material_units'  AS object, count(*)      FROM canonical_material_units
UNION ALL
SELECT 'genealogy_edges'           AS object, count(*)      FROM genealogy_edges
UNION ALL
SELECT 'canonical_genealogy_edges' AS object, count(*)      FROM canonical_genealogy_edges;
"@

Run-Sql "3. Imported heats present in base table (expect ~1802)" @"
SELECT material_unit_type,
       coalesce(source_system,'(null)') AS source_system,
       count(*) AS n
FROM material_units
WHERE material_unit_type = 'Heat'
GROUP BY material_unit_type, source_system
ORDER BY n DESC;
"@

Section "Read"
Write-Host "     Query 1 is decisive:" -ForegroundColor Gray
Write-Host "       both canonical_* = VIEW -> imports already canonical everywhere; Stage C = retire the dead ladder." -ForegroundColor Gray
Write-Host "       either canonical_* = TABLE -> drift; Stage C = drop the table + recreate as the script-310 view." -ForegroundColor Gray
Write-Host "     Query 2: if canonical count matches base count, the view is live and reflecting our imports." -ForegroundColor Gray
