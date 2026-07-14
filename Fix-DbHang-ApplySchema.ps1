#requires -Version 5.1
<#
================================================================================
 PPIQ FIX  -  Unhang the DB, verify/apply the M1-06 alerting schema (safe, fast)
================================================================================
 RUN:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-DbHang-ApplySchema.ps1

 This NEVER hangs: every psql call uses connect_timeout=5 and statement_timeout=5s,
 so it errors in seconds instead of blocking. It:
   1. connectivity probe (SELECT 1) with a hard 5s ceiling
   2. if unreachable -> tells you to stop the API/web and restart the PG service
   3. if reachable   -> checks whether alert_rules/plant_data_log/function exist;
                        applies 730_*.sql ONLY if missing; verifies.

 Root cause of the earlier hang: psql conninfo had no connect_timeout, so when the
 server stopped accepting new connections (API pool + startup migration racing on
 the same new tables), psql waited forever on connect. Fixed here.
================================================================================
#>
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Info($m){ Write-Host "[i] $m" -ForegroundColor Cyan }
function Write-Ok  ($m){ Write-Host "[+] $m" -ForegroundColor Green }
function Write-Warn($m){ Write-Host "[!] $m" -ForegroundColor Yellow }
function Write-Err ($m){ Write-Host "[x] $m" -ForegroundColor Red }

function Find-Psql {
    $cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $c = Get-ChildItem 'C:\Program Files\PostgreSQL' -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'bin\psql.exe' } | Where-Object { Test-Path $_ } | Sort-Object -Descending
    if ($c -and $c.Count -ge 1) { return $c[0] }
    return $null
}
$Psql = Find-Psql
if (-not $Psql) { Write-Err "psql.exe not found."; exit 1 }
Write-Info "psql: $Psql"

# connect_timeout in the conninfo is the key fix - psql cannot hang on connect.
$ConnInfo = "host=127.0.0.1 port=5432 dbname=ppiq_app user=ppiq_dev password=ppiq_dev_local_only connect_timeout=5"
$Guard = "SET statement_timeout='5s'; SET lock_timeout='2s';"

function Run-Sql([string]$sql) {
    $saveEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $out = & $Psql $ConnInfo -X -q -t -A -P pager=off -c ($Guard + $sql) 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $saveEap
    return [pscustomobject]@{ Code = $code; Out = (($out | Out-String).Trim()) }
}

# ---- 1. connectivity probe (hard 5s ceiling via connect_timeout) -------------
Write-Info "Probing ppiq_app (max ~5s)..."
$probe = Run-Sql "SELECT 1;"
if ($probe.Code -ne 0 -or $probe.Out -notmatch '1') {
    Write-Err "Cannot connect to ppiq_app within 5s. The server is not accepting connections."
    Write-Host ""
    Write-Warn "Do this, in order:"
    Write-Host "  1) Stop the API + web (they hold the pool):"
    Write-Host "       Get-Process psql -ErrorAction SilentlyContinue | Stop-Process -Force"
    Write-Host "       Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe'\" | Where-Object { \$_.CommandLine -like '*PlantProcess*' } | ForEach-Object { Stop-Process -Id \$_.ProcessId -Force }"
    Write-Host "  2) Restart the PostgreSQL service (clears every stuck session; needs an ADMIN PowerShell):"
    Write-Host "       Restart-Service postgresql-x64-16"
    Write-Host "     (or, elevated:  net stop postgresql-x64-16 ; net start postgresql-x64-16 )"
    Write-Host "  3) Re-run this script."
    $svc = Get-Service -Name 'postgresql-x64-16' -ErrorAction SilentlyContinue
    if ($svc) { Write-Info ("PG service status: " + $svc.Status) }
    exit 1
}
Write-Ok "Connected. Server is healthy."

# ---- 2. is the schema already there? ----------------------------------------
$state = Run-Sql "SELECT (to_regclass('public.alert_rules') IS NOT NULL)::int, (to_regclass('public.plant_data_log') IS NOT NULL)::int, (EXISTS(SELECT 1 FROM pg_proc WHERE proname='ppiq_evaluate_alert_rules'))::int;"
$p = ($state.Out -split '\|') | ForEach-Object { $_.Trim() }
$haveAll = ($p.Count -ge 3 -and $p[0] -eq '1' -and $p[1] -eq '1' -and $p[2] -eq '1')
if ($haveAll) {
    Write-Ok "Schema already present: alert_rules, plant_data_log, ppiq_evaluate_alert_rules() all exist."
    Write-Info "(The API startup migration likely applied 730_ already. Nothing to do.)"
    $smoke = Run-Sql "SELECT public.ppiq_evaluate_alert_rules();"
    if ($smoke.Code -eq 0) { Write-Ok "Evaluator callable; returned $($smoke.Out)." }
    exit 0
}
Write-Warn "Schema not fully present (alert_rules=$($p[0]) plant_data_log=$($p[1]) fn=$($p[2])). Applying 730_..."

# ---- 3. apply 730 (idempotent) ----------------------------------------------
$RepoRoot = (Get-Location).Path
$SqlFile = Join-Path $RepoRoot 'Backend\database\scripts\730_alert_rules_and_plant_data_log.sql'
if (-not (Test-Path $SqlFile)) { Write-Err "Missing $SqlFile - re-run Apply-M1-06a first to write it."; exit 1 }

$saveEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$applyOut = & $Psql $ConnInfo -v ON_ERROR_STOP=1 -X -q -f $SqlFile 2>&1
$applyCode = $LASTEXITCODE
$ErrorActionPreference = $saveEap
if ($applyCode -ne 0) {
    Write-Err "Apply failed (exit $applyCode):"
    Write-Host ($applyOut | Out-String)
    Write-Warn "If it mentions a lock, a stuck session still holds it - restart the PG service (admin) and re-run."
    exit 1
}
Write-Ok "730_ applied."

$verify = Run-Sql "SELECT (to_regclass('public.alert_rules') IS NOT NULL)::int, (to_regclass('public.plant_data_log') IS NOT NULL)::int, (EXISTS(SELECT 1 FROM pg_proc WHERE proname='ppiq_evaluate_alert_rules'))::int;"
$v = ($verify.Out -split '\|') | ForEach-Object { $_.Trim() }
if ($v.Count -ge 3 -and $v[0] -eq '1' -and $v[1] -eq '1' -and $v[2] -eq '1') {
    Write-Ok "Verified: alerting schema fully present. M1-06a complete."
} else {
    Write-Err "Post-apply verify incomplete: $($verify.Out)"
    exit 1
}
