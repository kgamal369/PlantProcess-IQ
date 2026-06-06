[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [string]$ConfigPath = "tools\edge-agent\edge-agent.sample.json",
    [switch]$Push
)

$ErrorActionPreference = "Stop"
Push-Location $ProjectRoot
try {
    $agent = Join-Path $ProjectRoot "tools\edge-agent\ppiq-edge-agent.cjs"
    $config = Join-Path $ProjectRoot $ConfigPath

    if (-not (Test-Path $agent)) { throw "Missing edge agent script: $agent" }
    if (-not (Test-Path $config)) { throw "Missing edge agent config: $config" }

    $mode = if ($Push) { "--push" } else { "--dry-run" }
    node $agent --config=$config $mode --once
}
finally { Pop-Location }
