[CmdletBinding()]
param(
    [int]$Port = 5432
)

$ErrorActionPreference = "Stop"

$connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue

if (-not $connections) {
    Write-Host "No local listener found on port $Port. OK if PostgreSQL is container-internal only." -ForegroundColor Green
    exit 0
}

$bad = $connections | Where-Object {
    $_.LocalAddress -in @("0.0.0.0", "::", "[::]")
}

if ($bad) {
    $bad | Format-Table -AutoSize
    throw "PostgreSQL port $Port is publicly bound. It must be loopback/private only."
}

Write-Host "PostgreSQL port $Port is not publicly bound." -ForegroundColor Green