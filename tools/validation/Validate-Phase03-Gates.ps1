Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Push-Location (Resolve-Path "$PSScriptRoot\..\..")
try {
    node .\tools\validation\validate-phase03-gates.mjs
}
finally {
    Pop-Location
}