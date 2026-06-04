$ErrorActionPreference = "Stop"
$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$TestingRoot = "$RepoRoot\Documentation\testing"
$RunnerPath = "$RepoRoot\scripts\test\run-e2e-stable.ps1"
$StableMdPath = "$TestingRoot\Stable_E2E_Core.md"
$Latest = Get-ChildItem "$TestingRoot\S3C_StableE2ECoreManifest_*.csv" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $Latest) { throw "No S3C stable E2E manifest found." }
if (-not (Test-Path $RunnerPath)) { throw "Stable E2E runner was not created." }
if (-not (Test-Path $StableMdPath)) { throw "Stable E2E markdown was not created." }
$Rows = Import-Csv $Latest.FullName
if (@($Rows).Count -lt 1) { throw "Stable E2E manifest is empty." }
$Missing = New-Object System.Collections.Generic.List[string]
foreach ($R in $Rows) {
    if (-not (Test-Path (Join-Path $RepoRoot $R.RelativePath))) { $Missing.Add($R.RelativePath) | Out-Null }
}
if ($Missing.Count -gt 0) {
    Write-Host "Missing stable E2E spec files:" -ForegroundColor Red
    foreach ($M in $Missing) { Write-Host " - $M" -ForegroundColor Red }
    exit 1
}
Write-Host "[GREEN] S3C stable E2E core validation passed." -ForegroundColor Green
Write-Host "Stable specs: $(@($Rows).Count)" -ForegroundColor Yellow
