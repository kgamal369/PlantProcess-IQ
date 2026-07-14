#requires -Version 5.1
<#
================================================================================
 PPIQ INSPECT  -  Why won't PostgreSQL start? (no psql, cannot hang)
================================================================================
 RUN:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\Inspect-Postgres.ps1

 Shows: service status, postgres.exe processes, who is listening on 5432, and
 the TAIL of the newest server log - which tells us if PG is RECOVERING (wait)
 or STUCK (needs orphan cleanup + a clean start).
================================================================================
#>
$ErrorActionPreference = 'Continue'
Set-StrictMode -Version Latest

function Line($m){ Write-Host $m -ForegroundColor Cyan }

Line "===== 1) Service status ====="
$svc = Get-Service -Name 'postgresql-x64-16' -ErrorAction SilentlyContinue
if ($svc) { Write-Host ("  postgresql-x64-16 : " + $svc.Status) } else { Write-Host "  service not found by that name" }

Line "===== 2) postgres.exe processes (StartTime) ====="
$procs = Get-Process -Name 'postgres' -ErrorAction SilentlyContinue
if ($procs) {
    $procs | Select-Object Id, StartTime | Sort-Object StartTime | ForEach-Object {
        Write-Host ("  PID {0}  started {1}" -f $_.Id, $_.StartTime)
    }
    Write-Host ("  count: " + @($procs).Count)
} else { Write-Host "  none running" }

Line "===== 3) Listener on port 5432 ====="
$conn = Get-NetTCPConnection -LocalPort 5432 -State Listen -ErrorAction SilentlyContinue
if ($conn) {
    $conn | ForEach-Object {
        $op = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue
        Write-Host ("  listening: {0}:{1}  PID {2} ({3})" -f $_.LocalAddress, $_.LocalPort, $_.OwningProcess, ($op.ProcessName))
    }
} else { Write-Host "  nothing listening on 5432 (postmaster not up)" }

Line "===== 4) Data directory + newest server log ====="
$dataDir = $null
$wsvc = Get-CimInstance Win32_Service -Filter "Name='postgresql-x64-16'" -ErrorAction SilentlyContinue
if ($wsvc -and $wsvc.PathName) {
    $m = [regex]::Match($wsvc.PathName, '-D\s+"([^"]+)"')
    if (-not $m.Success) { $m = [regex]::Match($wsvc.PathName, '-D\s+(\S+)') }
    if ($m.Success) { $dataDir = $m.Groups[1].Value }
}
if (-not $dataDir) {
    foreach ($c in @('C:\Program Files\PostgreSQL\16\data')) { if (Test-Path $c) { $dataDir = $c; break } }
}
Write-Host ("  data dir: " + $(if ($dataDir) { $dataDir } else { '<unknown>' }))

if ($dataDir -and (Test-Path $dataDir)) {
    $pidfile = Join-Path $dataDir 'postmaster.pid'
    if (Test-Path $pidfile) {
        Write-Host "  postmaster.pid present. First lines:"
        Get-Content $pidfile -TotalCount 4 | ForEach-Object { Write-Host ("    " + $_) }
    } else { Write-Host "  no postmaster.pid (postmaster not currently holding the dir)" }

    $logDirs = @((Join-Path $dataDir 'log'), (Join-Path $dataDir 'pg_log'))
    $logFile = $null
    foreach ($ld in $logDirs) {
        if (Test-Path $ld) {
            $f = Get-ChildItem $ld -Filter '*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($f) { $logFile = $f.FullName; break }
        }
    }
    if ($logFile) {
        Write-Host ("  newest log: " + $logFile)
        Line "----- last 40 log lines -----"
        Get-Content $logFile -Tail 40 | ForEach-Object { Write-Host ("  " + $_) }
    } else {
        Write-Host "  no log file found under log\ or pg_log\ (logging may go to Windows Event Log)."
        Write-Host "  Check: Get-EventLog -LogName Application -Source PostgreSQL* -Newest 20"
    }
}

Line "===== READING THE RESULT ====="
Write-Host "  - Log says 'database system is ready to accept connections'  -> PG is UP; retry the Fix pack."
Write-Host "  - Log says 'redo in progress' / 'database system is starting up' -> RECOVERING; WAIT, re-run this in a few min."
Write-Host "  - Log shows a repeating FATAL / 'could not' / 'lock file' -> STUCK; paste it and I will give the exact cleanup."
