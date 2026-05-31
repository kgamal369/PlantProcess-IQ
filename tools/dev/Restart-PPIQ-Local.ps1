# Local restart helper using kill-by-port.
# PPIQ-T284.

[CmdletBinding()]
param(
    [int]$ApiPort = 5063,
    [int]$WebPort = 5173,
    [int]$ReportPort = 9323
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $PSCommandPath)

& "$Root\tools\dev\Stop-PPIQ-Port.ps1" -Port $ApiPort
& "$Root\tools\dev\Stop-PPIQ-Port.ps1" -Port $WebPort
& "$Root\tools\dev\Stop-PPIQ-Port.ps1" -Port $ReportPort

Write-Host "Ports freed: $ApiPort, $WebPort, $ReportPort"
