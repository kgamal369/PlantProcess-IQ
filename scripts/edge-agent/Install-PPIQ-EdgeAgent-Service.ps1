[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [string]$ServiceName = "PPIQEdgeAgent",
    [string]$ConfigPath = "tools\edge-agent\edge-agent.sample.json"
)

$ErrorActionPreference = "Stop"
$agent = Join-Path $ProjectRoot "tools\edge-agent\ppiq-edge-agent.cjs"
$config = Join-Path $ProjectRoot $ConfigPath
$node = (Get-Command node.exe -ErrorAction Stop).Source

Write-Host "PPIQ edge agent service install guidance" -ForegroundColor Cyan
Write-Host "Service name : $ServiceName"
Write-Host "Node        : $node"
Write-Host "Agent       : $agent"
Write-Host "Config      : $config"
Write-Host ""
Write-Host "This script intentionally does not force-install a Windows service." -ForegroundColor Yellow
Write-Host "Use your approved service wrapper, for example NSSM, WinSW, or enterprise deployment tooling."
Write-Host "Command payload:"
Write-Host "node $agent --config=$config --push --once"
