$ErrorActionPreference = "Stop"
$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$Inner = "$RepoRoot\scripts\test\validate-current-green.ps1"
if (-not (Test-Path $Inner)) { throw "Missing validate-current-green.ps1" }
& $Inner
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host ""
Write-Host "[GREEN] validate-current-green completed successfully." -ForegroundColor Green
