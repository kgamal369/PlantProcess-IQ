#requires -Version 5.1
<#
================================================================================
 PPIQ DIAG  -  Why is the schema apply (or any psql) hanging on ppiq_app?
================================================================================
 RUN:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\Diagnose-DbApplyHang.ps1
   ... -KillBlockers   (terminate idle-in-transaction + blocking backends, then you re-run M1-06a)

 A CREATE TABLE IF NOT EXISTS that hangs for minutes is ALWAYS a lock wait, not
 slow DDL. This shows every ppiq_app session, what each is waiting on, and the
 blocking tree (who is holding the lock). It CANNOT hang itself: lock_timeout=2s,
 statement_timeout=5s.

 Common causes here:
   - The API startup is applying the numbered scripts (holds ACCESS EXCLUSIVE).
   - A prior psql left a session "idle in transaction" holding a lock.
   - Two apply runs racing on the same new table.

 -KillBlockers terminates: (a) sessions idle-in-transaction, and (b) any session
 that is blocking another. It never touches your own diagnostic session. After it
 runs, stop the API if it is mid-migration, then re-run Apply-M1-06a.
================================================================================
#>
param(
    [switch]$KillBlockers
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Info($m){ Write-Host "[i] $m" -ForegroundColor Cyan }
function Write-Ok  ($m){ Write-Host "[+] $m" -ForegroundColor Green }
function Write-Warn($m){ Write-Host "[!] $m" -ForegroundColor Yellow }
function Write-Err ($m){ Write-Host "[x] $m" -ForegroundColor Red }

function Find-Psql {
    $cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $candidates = Get-ChildItem 'C:\Program Files\PostgreSQL' -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'bin\psql.exe' } |
        Where-Object { Test-Path $_ } | Sort-Object -Descending
    if ($candidates -and $candidates.Count -ge 1) { return $candidates[0] }
    return $null
}
$Psql = Find-Psql
if (-not $Psql) { Write-Err "psql.exe not found."; exit 1 }
Write-Info "psql: $Psql"

$ConnInfo = "host=127.0.0.1 port=5432 dbname=ppiq_app user=ppiq_dev password=ppiq_dev_local_only"
$Guard = "SET lock_timeout='2s'; SET statement_timeout='5s';"

function Run-Sql([string]$sql) {
    $saveEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $out = & $Psql $ConnInfo -X -q -P pager=off -c ($Guard + $sql) 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $saveEap
    return [pscustomobject]@{ Code = $code; Out = ($out | Out-String) }
}

Write-Info "Active/blocked sessions on ppiq_app:"
$activity = Run-Sql @"
SELECT pid,
       state,
       coalesce(wait_event_type,'') AS wait_type,
       coalesce(wait_event,'')      AS wait_event,
       date_trunc('second', now() - query_start) AS running_for,
       left(regexp_replace(query, '\s+', ' ', 'g'), 70) AS query
FROM pg_stat_activity
WHERE datname = 'ppiq_app' AND pid <> pg_backend_pid()
ORDER BY query_start;
"@
Write-Host $activity.Out

Write-Info "Blocking tree (who is holding the lock the others wait on):"
$blockers = Run-Sql @"
SELECT pid AS blocked_pid,
       pg_blocking_pids(pid) AS blocked_by,
       state,
       left(regexp_replace(query, '\s+', ' ', 'g'), 60) AS query
FROM pg_stat_activity
WHERE datname = 'ppiq_app' AND cardinality(pg_blocking_pids(pid)) > 0;
"@
Write-Host $blockers.Out

if (-not $KillBlockers) {
    Write-Warn "Run again with -KillBlockers to terminate idle-in-transaction + blocking sessions."
    Write-Info "TIP: if the blocker query is a CREATE/ALTER from the API startup migration, let the API finish"
    Write-Info "     OR stop the API, then re-run Apply-M1-06a. If it is 'idle in transaction', -KillBlockers clears it."
    exit 0
}

Write-Warn "Terminating idle-in-transaction and blocking backends (not this session)..."
$kill = Run-Sql @"
SELECT pg_terminate_backend(pid), pid, state
FROM pg_stat_activity
WHERE datname = 'ppiq_app'
  AND pid <> pg_backend_pid()
  AND (
        state = 'idle in transaction'
        OR pid IN (
             SELECT unnest(pg_blocking_pids(p.pid))
             FROM pg_stat_activity p
             WHERE p.datname = 'ppiq_app' AND cardinality(pg_blocking_pids(p.pid)) > 0
        )
      );
"@
Write-Host $kill.Out
if ($kill.Code -eq 0) {
    Write-Ok "Blockers cleared. Now: if the API is mid-migration stop it, then re-run Apply-M1-06a-AlertingSchema.ps1."
} else {
    Write-Err "Termination query returned non-zero; output above."
}
