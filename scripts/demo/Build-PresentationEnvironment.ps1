# ============================================================================
# Build-PresentationEnvironment.ps1
# Creates a fully populated, DISPOSABLE presentation environment WITHOUT
# touching main or your dev database:
#
#   1. Clones the cleaned ppiq_app -> ppiq_presentation (TEMPLATE clone).
#      The clone inherits ALL of yesterday's Rule-1 cleanup PLUS the 320 real
#      correlation results / 375 gated runs the engine computed on this
#      dataset - findings pages populate immediately, honestly.
#   2. Applies the repo seed files ON THE CLONE (months-of-plant data spine:
#      units, observations, quality events, genealogy, risk scores, routes).
#   3. Writes env\profiles\presentation.env (local.env with the DB swapped).
#   4. Reports every demo-named row the seeds bring back - into the CLONE
#      only; your dev DB never sees them.
#
# RETURN PATH (the whole point - zero cleanup):
#      git checkout main
#      .\scripts\run\start-api.ps1 -Profile local
#   ...and you are on pristine dev state. Drop the clone whenever:
#      DROP DATABASE ppiq_presentation;   (or keep it for marketing screenshots)
#
# Run from repo root (STOP THE API FIRST - the template clone needs
# ppiq_app connection-free for a few seconds):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-PresentationEnvironment.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-PresentationEnvironment.ps1 -Force   (drop + rebuild existing clone)
# ============================================================================
[CmdletBinding()]
param(
    [switch]$Force,
    [string]$PresentationDb = 'ppiq_presentation'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

$RepoRoot = (Get-Location).Path
$SeedDir  = Join-Path $RepoRoot 'Backend\database\seed'
$EnvSrc   = Join-Path $RepoRoot 'env\profiles\local.env'
$EnvDst   = Join-Path $RepoRoot 'env\profiles\presentation.env'
$Stamp    = Get-Date -Format 'yyyyMMdd_HHmmss'
$OutFile  = Join-Path $RepoRoot ("PresentationEnv_" + $Stamp + ".txt")
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }

# ---- locate psql -----------------------------------------------------------
$Psql = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $Psql = $cmd.Source } else {
    $c = @(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if ($c.Count -gt 0) { $Psql = $c[0].FullName }
}
if (-not $Psql) { Write-Host "[FAIL] psql not found." -ForegroundColor Red; exit 1 }
$env:PGPASSWORD = 'ppiq_dev_local_only'
function PgAdmin([string]$q) {
    return @(& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d postgres -w -X -A -t -c $q 2>&1)
}
function PgPres([string]$q) {
    $o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $PresentationDb -w -X -A -t -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { return 'n/a' }
    $l = @($o | Where-Object { $_ -and ($_.ToString().Trim() -ne '') }) | Select-Object -First 1
    if ($null -eq $l) { return '0' }
    return $l.ToString().Trim()
}

W ("PPIQ PRESENTATION ENVIRONMENT BUILD - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ("Source: ppiq_app (cleaned)  ->  Target: " + $PresentationDb)
W ("=" * 78)

# ---- 1. clone --------------------------------------------------------------
$exists = (PgAdmin ("SELECT 1 FROM pg_database WHERE datname = '" + $PresentationDb + "';") | Where-Object { $_ -eq '1' })
if ($exists) {
    if ($Force) {
        W ("[CLONE] dropping existing " + $PresentationDb + " (-Force)...")
        PgAdmin ("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '" + $PresentationDb + "';") | Out-Null
        $r = PgAdmin ("DROP DATABASE " + $PresentationDb + ";")
        if ($LASTEXITCODE -ne 0) { $r | ForEach-Object { W ("  " + $_) }; W "[ABORT]"; exit 1 }
    } else {
        W ("[ABORT] " + $PresentationDb + " already exists. Re-run with -Force to rebuild it.")
        exit 1
    }
}

$active = PgAdmin "SELECT COUNT(*) FROM pg_stat_activity WHERE datname = 'ppiq_app' AND pid <> pg_backend_pid();"
if ([int]($active | Select-Object -First 1) -gt 0) {
    W ("[ABORT] " + ($active | Select-Object -First 1) + " active connection(s) to ppiq_app (the API?).")
    W "        Stop the API, re-run this script, then start the API with -Profile presentation."
    [System.IO.File]::WriteAllText($OutFile, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
    exit 1
}

W ("[CLONE] CREATE DATABASE " + $PresentationDb + " TEMPLATE ppiq_app ...")
$r = PgAdmin ("CREATE DATABASE " + $PresentationDb + " TEMPLATE ppiq_app OWNER ppiq_dev;")
if ($LASTEXITCODE -ne 0) { $r | ForEach-Object { W ("  " + $_) }; W "[ABORT] clone failed."; exit 1 }
W "[CLONE] OK - the clone carries all Rule-1 cleanup + the 320 real engine results."
W ""

# ---- 2. seed the clone -------------------------------------------------------
if (-not (Test-Path $SeedDir)) {
    W ("[WARN] seed dir missing: " + $SeedDir + " - recover with: git checkout -- Backend/database/seed")
} else {
    $seeds = @(Get-ChildItem -Path $SeedDir -Filter *.sql -File | Sort-Object Name)
    $ok = 0; $failed = 0
    foreach ($s in $seeds) {
        if ($s.Name -like '*ed25519*') { W ("[SEED] SKIP " + $s.Name); continue }
        $out = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $PresentationDb -w -X -v ON_ERROR_STOP=1 -f $s.FullName 2>&1
        if ($LASTEXITCODE -eq 0) { $ok++; W ("[SEED] OK     " + $s.Name) }
        else {
            $failed++; W ("[SEED] FAILED " + $s.Name + " (independent - continuing):")
            @($out | Select-Object -First 3) | ForEach-Object { W ("         " + $_) }
        }
    }
    W ("[SEED] applied " + $ok + " / failed " + $failed)
}
W ""

# ---- 3. counts + residue report ---------------------------------------------
W ("---- " + $PresentationDb + " state ----")
foreach ($t in @('material_units', 'parameter_observations', 'quality_events', 'genealogy_edges', 'defect_catalogs', 'parameter_definitions', 'ml_correlation_results_v2', 'page_definitions')) {
    W ("    " + $t.PadRight(28) + " " + (PgPres ("SELECT COUNT(*) FROM " + $t + ";")))
}
W ""
W "---- demo-named residue the seeds brought back (CLONE ONLY - rename what will be on screen) ----"
$r = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $PresentationDb -w -X -A -t -F ' | ' -c "SELECT site_code, site_name FROM sites ORDER BY site_code;" 2>&1
@($r) | Where-Object { $_ } | ForEach-Object { W ("    site: " + $_) }
$r = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $PresentationDb -w -X -A -t -c "SELECT DISTINCT source_system FROM material_units ORDER BY 1;" 2>&1
@($r) | Where-Object { $_ } | ForEach-Object { W ("    material_units.source_system: " + $_) }
W ""

# ---- 4. presentation env profile ---------------------------------------------
if (Test-Path $EnvSrc) {
    $envText = [System.IO.File]::ReadAllText($EnvSrc, [System.Text.Encoding]::UTF8)
    $envText = $envText.Replace('ppiq_app', $PresentationDb)
    [System.IO.File]::WriteAllText($EnvDst, $envText, (New-Object System.Text.UTF8Encoding($false)))
    W ("[ENV] wrote " + $EnvDst + " (local.env with ppiq_app -> " + $PresentationDb + ")")
} else {
    W ("[WARN] " + $EnvSrc + " not found - create presentation.env manually (DB name swap only).")
}

W ""
W ("=" * 78)
W "RUNBOOK - into the presentation:"
W "  git checkout -b presentation        # for any presentation-only tweaks"
W "  .\scripts\run\start-api.ps1 -Profile presentation"
W "  (frontend unchanged - it talks to the API, the API picks the DB)"
W "  In the product: reindex the assistant (POST /api/assistant/reindex once,"
W "  from the admin surface); create 1-2 alert rules in UI-4; compose your"
W "  dashboards in UI-2 tonight - and build ONE live in the meeting."
W ""
W "RUNBOOK - back to real work after (the zero-cleanup promise):"
W "  git checkout main"
W "  .\scripts\run\start-api.ps1 -Profile local"
W ("  -- optional, whenever: DROP DATABASE " + $PresentationDb + ";  (or keep for marketing)")

[System.IO.File]::WriteAllText($OutFile, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("[DONE] Report -> " + $OutFile) -ForegroundColor Green
exit 0
