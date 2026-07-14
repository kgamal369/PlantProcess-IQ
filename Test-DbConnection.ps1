#requires -Version 5.1
<#
================================================================================
 PPIQ TEST  -  Decisive ppiq_app connection test (CANNOT hang - 15s hard kill)
================================================================================
 RUN:
   powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-DbConnection.ps1

 Isolates the failure:
   - Test-NetConnection : is 5432 reachable at the TCP level?
   - psql -w (NEVER prompt) + PGPASSWORD + explicit flags, wrapped in a 15s
     wall-clock kill. -w turns a hidden password prompt into an instant error
     instead of an infinite wait.
 Outcomes:
   "1" / ok           -> connection works; the earlier hang was transient, retry Fix.
   auth error         -> password/pg_hba problem (I will fix the conninfo/creds).
   times out at 15s   -> below-libpq network/accept issue (I will pivot there).
================================================================================
#>
$ErrorActionPreference = 'Continue'
Set-StrictMode -Version Latest

function Find-Psql {
    $cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $c = Get-ChildItem 'C:\Program Files\PostgreSQL' -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'bin\psql.exe' } | Where-Object { Test-Path $_ } | Sort-Object -Descending
    if ($c -and $c.Count -ge 1) { return $c[0] }
    return $null
}
$Psql = Find-Psql
if (-not $Psql) { Write-Host "[x] psql.exe not found." -ForegroundColor Red; exit 1 }
Write-Host "[i] psql: $Psql" -ForegroundColor Cyan

# ---- 1. TCP reachability -----------------------------------------------------
Write-Host "[i] Testing TCP 127.0.0.1:5432 ..." -ForegroundColor Cyan
$tcp = Test-NetConnection -ComputerName 127.0.0.1 -Port 5432 -WarningAction SilentlyContinue
Write-Host ("    TCP reachable: " + $tcp.TcpTestSucceeded)

# ---- 2. psql with -w, explicit flags, HARD 15s wall-clock kill ---------------
Write-Host "[i] psql -w SELECT 1 (15s hard limit) ..." -ForegroundColor Cyan
$job = Start-Job -ScriptBlock {
    param($psql, $pw)
    $env:PGPASSWORD = $pw
    $env:PGCONNECT_TIMEOUT = '5'
    $out = & $psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d ppiq_app -w -X -A -t -c "SELECT 1 AS ok;" 2>&1
    "$out"
    "EXITCODE=$LASTEXITCODE"
} -ArgumentList $Psql, 'ppiq_dev_local_only'

if (Wait-Job $job -Timeout 15) {
    $res = Receive-Job $job
    Write-Host "----- psql result -----"
    $res | ForEach-Object { Write-Host ("    " + $_) }
    Remove-Job $job -Force
    $joined = ($res -join "`n")
    if ($joined -match '(^|\D)1($|\D)' -and $joined -match 'EXITCODE=0') {
        Write-Host "[+] Connection WORKS. The earlier hang was transient - re-run Fix-DbHang-ApplySchema.ps1." -ForegroundColor Green
    } elseif ($joined -match 'password|authentication|no password supplied|role .* does not exist|pg_hba') {
        Write-Host "[x] AUTH problem (password / pg_hba / role). Paste the line above - I will correct the credentials." -ForegroundColor Red
    } elseif ($joined -match 'database .* does not exist') {
        Write-Host "[x] Database ppiq_app does not exist under this server. Paste it - we may be pointed at the wrong cluster." -ForegroundColor Red
    } else {
        Write-Host "[!] Unexpected result - paste the lines above." -ForegroundColor Yellow
    }
} else {
    Stop-Job $job; Remove-Job $job -Force
    Write-Host "[x] psql did NOT return within 15s even with -w (no prompt possible)." -ForegroundColor Red
    Write-Host "    -> Not a password prompt. TCP reachable=$($tcp.TcpTestSucceeded)."
    Write-Host "    -> If TCP reachable but psql hangs: the backend accepts the socket but stalls before auth."
    Write-Host "       Likely a saturated/limited connection state. Paste this whole output; next step is to"
    Write-Host "       check max_connections vs active count via a different path."
}
