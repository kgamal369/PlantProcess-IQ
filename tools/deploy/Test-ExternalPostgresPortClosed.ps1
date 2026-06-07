[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ServerHost,

    [int]$Port = 5432,

    [int]$TimeoutMilliseconds = 3000
)

$ErrorActionPreference = "Stop"

# PPIQ_REALIZATION_T015_EXTERNAL_POSTGRES_PROBE

Write-Host "Testing external Postgres exposure: $($ServerHost):$Port" -ForegroundColor Cyan

$client = New-Object System.Net.Sockets.TcpClient
$async = $client.BeginConnect($ServerHost, $Port, $null, $null)
$wait = $async.AsyncWaitHandle.WaitOne($TimeoutMilliseconds, $false)

if ($wait -and $client.Connected) {
    $client.Close()
    throw "PPIQ-T015 failed: external host can connect to Postgres port $Port on $ServerHost."
}

$client.Close()
Write-Host "PPIQ-T015 passed: external Postgres port is refused/timed out." -ForegroundColor Green
