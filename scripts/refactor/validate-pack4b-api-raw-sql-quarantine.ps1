$ErrorActionPreference = "Stop"
$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$DocRoot = "$RepoRoot\Documentation\refactor"
$Latest = Get-ChildItem "$DocRoot\Pack4B_ApiRawSqlQuarantineGate_*.csv" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $Latest) { throw "No Pack4B API raw SQL quarantine gate CSV found." }
$Rows = Import-Csv $Latest.FullName
$Blockers = @($Rows | Where-Object { $_.Status -eq "BLOCKER" }).Count
$Missing = @($Rows | Where-Object { $_.Status -eq "MISSING" }).Count
$Warnings = @($Rows | Where-Object { $_.Status -eq "WARN" }).Count
Write-Host "[GREEN] Pack 4B API raw SQL quarantine validation executed." -ForegroundColor Green
Write-Host "Gate rows: $(@($Rows).Count)" -ForegroundColor Green
Write-Host "Blockers : $Blockers" -ForegroundColor Yellow
Write-Host "Missing  : $Missing" -ForegroundColor Yellow
Write-Host "Warnings : $Warnings" -ForegroundColor Yellow
if ($Blockers -gt 0 -or $Missing -gt 0 -or $Warnings -gt 0) { throw "Pack4B has raw SQL quarantine issues." }
