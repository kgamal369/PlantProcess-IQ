param()

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")

Push-Location $repoRoot
try {
    node .\tools\validation\validate-phase01-phase02-v5-gates.mjs
}
finally {
    Pop-Location
}