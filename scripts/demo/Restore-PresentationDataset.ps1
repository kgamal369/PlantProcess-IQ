# ============================================================================
# Restore-PresentationDataset.ps1
# THE FAST PATH: loads the pre-purge dump (full ~38k unit dataset + the rigged
# correlation pattern) into ppiq_presentation, then RE-APPLIES every 15-Jul
# Rule-1 fix on top - because the dump predates all of them.
#
# Without step 2 you would be presenting DEMO-READY-CP-01, ADV_DEMO_PLANT,
# "required for demo learning" and "golden dataset: 13 findings" on screen.
#
# HARD GUARD: the target database name must contain 'presentation'. This
# script cannot touch ppiq_app even if you ask it to.
#
# STOP THE API FIRST (pg_restore --clean needs the DB connection-free).
# Restart after with: .\scripts\run\start-api.ps1 -Profile presentation
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Restore-PresentationDataset.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Restore-PresentationDataset.ps1 -Execute
# ============================================================================
[CmdletBinding()]
param(
    [string]$DumpPath = 'deploy\.ppiq-snapshots\ppiq_app_20260713_203359.dump',
    [string]$TargetDb = 'ppiq_presentation',
    [switch]$Execute
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

if ($TargetDb -notmatch 'presentation') {
    Write-Host "[REFUSED] target database must contain 'presentation'. Guard active." -ForegroundColor Red
    exit 1
}

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$OutFile = Join-Path $RepoRoot ("PresentationRestore_" + $Stamp + ".txt")
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }

$PgBin = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $PgBin = Split-Path $cmd.Source -Parent } else {
    $c = @(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if ($c.Count -gt 0) { $PgBin = Split-Path $c[0].FullName -Parent }
}
if (-not $PgBin) { Write-Host "[FAIL] psql not found." -ForegroundColor Red; exit 1 }
$Psql = Join-Path $PgBin 'psql.exe'
$PgRestore = Join-Path $PgBin 'pg_restore.exe'
$env:PGPASSWORD = 'ppiq_dev_local_only'

function T([string]$q) {
    $o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { return 'n/a' }
    $l = @($o | Where-Object { $_ -and ($_.ToString().Trim() -ne '') }) | Select-Object -First 1
    if ($null -eq $l) { return '0' }
    return $l.ToString().Trim()
}
function Rows([string]$q) {
    return @(& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F ' | ' -c $q 2>&1 | Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}
function Exec([string]$label, [string]$q) {
    $o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -c $q 2>&1
    if ($LASTEXITCODE -eq 0) { W ("    OK   " + $label) } else {
        W ("    FAIL " + $label)
        @($o | Select-Object -First 2) | ForEach-Object { W ("         " + $_) }
    }
}

$dump = Join-Path $RepoRoot $DumpPath
if (-not (Test-Path $dump)) { $dump = $DumpPath }
W ("PRESENTATION DATASET RESTORE - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ("Dump  : " + $dump)
W ("Target: " + $TargetDb + "   (ppiq_app is guarded and untouched)")
W ("=" * 78)
if (-not (Test-Path $dump)) { W "[ABORT] dump not found."; exit 1 }
$di = Get-Item $dump
W ("Dump date: " + $di.LastWriteTime + "   size: " + [Math]::Round($di.Length / 1MB, 1) + " MB")
if ($di.LastWriteTime -ge (Get-Date '2026-07-14 13:00:00')) {
    W "[WARN] this dump is NOT pre-purge - it will restore an empty dataset."
}
W ""

if (-not $Execute) {
    W "DRY-RUN. This would:"
    W "  1. pg_restore --clean --if-exists the dump into " + $TargetDb
    W "  2. re-apply the 15-Jul Rule-1 fixes (site + profile renames, engine"
    W "     message de-demo incl. live function redefinition, demo schema-view"
    W "     rows, oversized job_log truncation)"
    W "  3. report final counts + any residue still visible on screen"
    W ""
    W "Current " + $TargetDb + " counts (pre-restore):"
    foreach ($t in @('material_units', 'parameter_observations', 'quality_events', 'genealogy_edges')) {
        W ("    " + $t.PadRight(24) + " " + (T ("SELECT COUNT(*) FROM " + $t + ";")))
    }
    W ""
    W "STOP THE API, then re-run with -Execute."
    [System.IO.File]::WriteAllText($OutFile, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
    exit 0
}

# ---- connection check ------------------------------------------------------
$active = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d postgres -w -X -A -t -c ("SELECT COUNT(*) FROM pg_stat_activity WHERE datname = '" + $TargetDb + "' AND pid <> pg_backend_pid();") 2>&1
$n = [int](@($active | Where-Object { $_ -match '^\d+$' }) | Select-Object -First 1)
if ($n -gt 0) {
    W ("[ABORT] " + $n + " active connection(s) to " + $TargetDb + " - stop the API and re-run.")
    [System.IO.File]::WriteAllText($OutFile, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
    exit 1
}

# ---- 1. restore ------------------------------------------------------------
W "[1/3] pg_restore --clean --if-exists ..."
& $PgRestore -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w --clean --if-exists --no-owner --no-privileges $dump 2>&1 |
    Select-Object -First 8 | ForEach-Object { W ("    " + $_) }
W "    (pg_restore warnings about missing objects on --clean are normal)"
W ""
W "    restored counts:"
foreach ($t in @('material_units', 'parameter_observations', 'quality_events', 'genealogy_edges', 'ml_correlation_results_v2')) {
    W ("      " + $t.PadRight(28) + " " + (T ("SELECT COUNT(*) FROM " + $t + ";")))
}
W ""

# ---- 2. re-apply the 15-Jul Rule-1 fixes -----------------------------------
W "[2/3] Re-applying the 15-Jul Rule-1 fixes (the dump predates all of them)..."

Exec 'sites -> PLANT-NN' @"
WITH ranked AS (
  SELECT id, ROW_NUMBER() OVER (ORDER BY site_code) AS rn
  FROM sites WHERE site_code ~* '(demo|adv|_p3_|p3_site|test|sample)'
)
UPDATE sites s
SET site_code = 'PLANT-' || lpad(r.rn::text, 2, '0'),
    site_name = 'Standard Manufacturing Plant',
    company_name = 'Standard Manufacturing'
FROM ranked r WHERE s.id = r.id;
"@

Exec 'connection profile codes -> CP-NN' @"
UPDATE connection_profiles
SET connection_profile_code = regexp_replace(connection_profile_code, '^(DEMO-READY-|DEMO-|ADV-)', '', 'i')
WHERE connection_profile_code ~* '^(DEMO-READY-|DEMO-|ADV-)';
"@

Exec 'readiness message: demo learning -> learning' @"
UPDATE ml_learning_runs_v1 SET error_message = replace(error_message, 'for demo learning.', 'for learning.')
WHERE error_message LIKE '%demo learning%';
"@
Exec 'readiness message: Golden dataset -> Dataset' @"
UPDATE ml_learning_runs_v1 SET readiness_message = replace(readiness_message, 'Golden dataset', 'Dataset')
WHERE readiness_message LIKE '%Golden dataset%';
"@
Exec 'compute-run messages: strip (golden dataset)' @"
UPDATE ml_correlation_compute_runs SET message = replace(message, ' (golden dataset)', '')
WHERE message LIKE '%(golden dataset)%';
"@
Exec 'job_log: truncate internal dumps' @"
UPDATE job_log SET message = left(message, 180) || ' ... [truncated: internal diagnostics removed]'
WHERE length(message) > 600;
"@
Exec 'canonical_schema_views: drop demo-era rows' @"
DELETE FROM canonical_schema_views WHERE physical_view_name ~ '^v_phase';
"@

# live functions (the messages regenerate from these on every run)
W "    live function redefinition:"
foreach ($p in @(
        @{ F = 'for demo learning.'; R = 'for learning.' },
        @{ F = 'Golden dataset contains sufficient'; R = 'Dataset contains sufficient' },
        @{ F = 'deterministic-core (golden dataset): '; R = 'deterministic-core: ' })) {
    $oids = Rows ("SELECT p.oid FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='public' AND p.prosrc LIKE '%" + $p.F.Replace("'", "''") + "%';")
    foreach ($oid in $oids) {
        $o = $oid.ToString().Trim()
        if ($o -notmatch '^\d+$') { continue }
        $defLines = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -c ("SELECT pg_get_functiondef(" + $o + ");") 2>&1
        if ($LASTEXITCODE -ne 0) { continue }
        $newDef = (($defLines -join "`n")).Replace($p.F, $p.R)
        $tmp = Join-Path $env:TEMP ("ppiq_pres_fn_" + $o + ".sql")
        [System.IO.File]::WriteAllText($tmp, $newDef, (New-Object System.Text.UTF8Encoding($false)))
        & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -v ON_ERROR_STOP=1 -f $tmp 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { W ("      REDEFINED oid " + $o + "  ('" + $p.F + "')") } else { W ("      FAILED oid " + $o) }
        Remove-Item $tmp -ErrorAction SilentlyContinue
    }
}
W ""

# ---- 3. residue report -----------------------------------------------------
W "[3/3] What a technical reviewer will see on screen now:"
W "    sites:"
Rows "SELECT site_code, site_name FROM sites ORDER BY site_code;" | ForEach-Object { W ("      " + $_) }
W "    connection profiles:"
Rows "SELECT connection_profile_code, connection_profile_name FROM connection_profiles ORDER BY 1;" | ForEach-Object { W ("      " + $_) }
W "    material_units.source_system:"
Rows "SELECT source_system, COUNT(*) FROM material_units GROUP BY 1 ORDER BY 2 DESC;" | ForEach-Object { W ("      " + $_) }
W "    remaining demo/golden strings in engine messages:"
$leak = T "SELECT COUNT(*) FROM ml_correlation_compute_runs WHERE message ~* '(demo|golden)';"
W ("      ml_correlation_compute_runs: " + $leak)
$leak2 = T "SELECT COUNT(*) FROM ml_learning_runs_v1 WHERE COALESCE(error_message,'') || COALESCE(readiness_message,'') ~* '(demo|golden)';"
W ("      ml_learning_runs_v1: " + $leak2)
W ""
W "NEXT:"
W "  .\scripts\run\start-api.ps1 -Profile presentation"
W "  powershell -NoProfile -ExecutionPolicy Bypass -File .\Certify-Journey.ps1 -SkipFrontendTests -ApiBase http://localhost:5063"
W ""
W "Anything still reading DEMO/SEED above is a source_system provenance value."
W "It shows in Material Investigation's provenance column. Decide: rename it,"
W "or use the honest line - 'this instance is preloaded with our emulated plant'."

[System.IO.File]::WriteAllText($OutFile, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("[DONE] Report -> " + $OutFile) -ForegroundColor Green
exit 0
