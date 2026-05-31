# PPIQ-T284
# Stops the process owning a TCP port and prints PID + StartTime proof.

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [int]$Port,

    [switch]$WhatIfOnly
)

$ErrorActionPreference = "Stop"

$connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue

if(-not $connections){
    Write-Host "OK: no listener found on port $Port" -ForegroundColor Green
    exit 0
}

$pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique

foreach($pidValue in $pids){
    $proc = Get-Process -Id $pidValue -ErrorAction SilentlyContinue

    if($null -eq $proc){
        Write-Host "WARN: PID $pidValue no longer exists" -ForegroundColor Yellow
        continue
    }

    $start = $null
    try { $start = $proc.StartTime } catch { $start = "(unavailable)" }

    Write-Host "Port $Port owner: PID=$pidValue Process=$($proc.ProcessName) StartTime=$start" -ForegroundColor Yellow

    if($WhatIfOnly){
        continue
    }

    Stop-Process -Id $pidValue -Force
    Write-Host "Stopped PID $pidValue on port $Port" -ForegroundColor Green
}
