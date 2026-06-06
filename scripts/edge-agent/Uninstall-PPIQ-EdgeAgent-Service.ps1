[CmdletBinding()]
param(
    [string]$ServiceName = "PPIQEdgeAgent"
)

$ErrorActionPreference = "Stop"
Write-Host "PPIQ edge agent service uninstall guidance" -ForegroundColor Cyan
Write-Host "Service name: $ServiceName"
Write-Host "Use the same approved service wrapper used during installation."
Write-Host "This script intentionally does not remove services blindly." -ForegroundColor Yellow
