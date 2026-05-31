# PPIQ-T284
# Prints the current owner PID + StartTime for a TCP port.

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [int]$Port
)

$connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue

if(-not $connections){
    Write-Host "No listener on port $Port" -ForegroundColor Yellow
    exit 1
}

$pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique

foreach($pidValue in $pids){
    $proc = Get-Process -Id $pidValue -ErrorAction SilentlyContinue
    if($null -eq $proc){
        Write-Host "PID $pidValue no longer exists" -ForegroundColor Yellow
        continue
    }

    $start = $null
    try { $start = $proc.StartTime } catch { $start = "(unavailable)" }

    Write-Host "Port $Port listener: PID=$pidValue Process=$($proc.ProcessName) StartTime=$start" -ForegroundColor Green
}
