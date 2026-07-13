#requires -Version 5.1
<#
  Diag-M1-07-PlantState.ps1
  -------------------------
  READ-ONLY. Reports the exact live state needed to plant a REAL 9.3x CRACK_LONG lift in the
  emulated source before we generate + import through the pipeline:
    1. material_units available to attach observations to (by type)
    2. quality_events landscape - does CRACK_LONG exist, and what is its base rate per heat?
    3. the parameter_definitions present (the 13-14, incl. the drivers superheat/cev)
    4. parameter_observations confirmed empty (the gate's blocker)
  With these numbers I generate superheat/cev values conditioned on the actual base rate so the
  odds ratio lands on 9.3x, not a lookalike.

  Launch (immune to execution policy / mark-of-the-web):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Diag-M1-07-PlantState.ps1
#>

[CmdletBinding()]
param(
    [string]$DbHost='127.0.0.1', [int]$Port=5432, [string]$Database='ppiq_app',
    [string]$User='ppiq_dev', [string]$Pass='ppiq_dev_local_only'
)

$ErrorActionPreference='Stop'
function Section($t){ Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Bad($t){ Write-Host "FAIL: $t" -ForegroundColor Red }

$psql=(Get-Command psql -ErrorAction SilentlyContinue).Source
if(-not $psql){ $c=Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -EA SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1; if($c){$psql=$c.FullName} }
if(-not $psql){ Bad "psql.exe not found."; exit 1 }
Write-Host "using psql: $psql" -ForegroundColor Gray
$env:PGPASSWORD=$Pass

function Run-Sql([string]$label,[string]$sql){
    Section $label
    $out = & $psql -h $DbHost -p $Port -d $Database -U $User -c $sql 2>&1
    if($LASTEXITCODE -ne 0){ Bad "query failed:"; $out | ForEach-Object { Write-Host "     $_" -ForegroundColor DarkYellow }; return }
    $out | ForEach-Object { Write-Host "     $_" }
}

Run-Sql "1. material_units by type (attach targets)" @"
SELECT material_unit_type, coalesce(source_system,'(null)') AS source_system, count(*) AS n
FROM material_units WHERE is_deleted = false
GROUP BY material_unit_type, source_system ORDER BY n DESC;
"@

Run-Sql "2a. quality_events columns (find the real defect/code column)" @"
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'quality_events'
ORDER BY ordinal_position;
"@

Run-Sql "2b. defect landscape (defect_code x material type, via defect_catalogs)" @"
SELECT coalesce(dc.defect_code,'(none)') AS defect_code,
       coalesce(mu.material_unit_type,'(none)') AS material_type,
       count(*) AS events, count(DISTINCT qe.material_unit_id) AS units
FROM quality_events qe
LEFT JOIN defect_catalogs dc ON dc.id = qe.defect_catalog_id
LEFT JOIN material_units  mu ON mu.id = qe.material_unit_id
GROUP BY dc.defect_code, mu.material_unit_type
ORDER BY events DESC LIMIT 25;
"@

Run-Sql "2c. CRACK_LONG base rate per grain (units with a CRACK defect vs total of that type)" @"
WITH crack AS (
    SELECT DISTINCT qe.material_unit_id
    FROM quality_events qe JOIN defect_catalogs dc ON dc.id = qe.defect_catalog_id
    WHERE dc.defect_code ILIKE '%CRACK%'
)
SELECT mu.material_unit_type,
       count(*) AS total_units,
       count(*) FILTER (WHERE mu.id IN (SELECT material_unit_id FROM crack)) AS units_with_crack,
       round( count(*) FILTER (WHERE mu.id IN (SELECT material_unit_id FROM crack))::numeric
              / NULLIF(count(*),0), 4) AS base_rate
FROM material_units mu
WHERE mu.is_deleted = false AND mu.material_unit_type IN ('Heat','Slab','Coil')
GROUP BY mu.material_unit_type
ORDER BY total_units DESC;
"@

Run-Sql "3. parameter_definitions present (drivers = thermal.true_superheat, chemistry.cev)" @"
SELECT parameter_code, display_name FROM parameter_definitions ORDER BY parameter_code;
"@

Run-Sql "4. parameter_observations count (expect 0 - the gate blocker)" @"
SELECT count(*) AS parameter_observations FROM parameter_observations;
"@

Section "Read"
Write-Host "     I need: total heats, heats_with_crack_long (base rate), and that the driver codes exist." -ForegroundColor Gray
Write-Host "     If CRACK_LONG already exists -> I condition superheat/cev on it to hit 9.3x." -ForegroundColor Gray
Write-Host "     If it does NOT exist -> the generator plants BOTH the drivers and CRACK_LONG defects" -ForegroundColor Gray
Write-Host "     consistently (both imported through the pipeline), so the engine discovers 9.3x organically." -ForegroundColor Gray
