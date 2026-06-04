$ErrorActionPreference = "Stop"
$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$DocRoot = "$RepoRoot\Documentation\deployment"
$Latest = Get-ChildItem "$DocRoot\Pack3B_ServerDeploymentGate_*.csv" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $Latest) { throw "No Pack3B server deployment gate CSV found." }
$Rows = Import-Csv $Latest.FullName
$Blockers = @($Rows | Where-Object { $_.Status -eq "BLOCKER" }).Count
$Missing = @($Rows | Where-Object { $_.Status -eq "MISSING" }).Count
$Warnings = @($Rows | Where-Object { $_.Status -eq "WARN" }).Count
Write-Host "[GREEN] Pack 3B server deployment manifest validation executed." -ForegroundColor Green
Write-Host "Gate rows: $(@($Rows).Count)" -ForegroundColor Green
Write-Host "Blockers : $Blockers" -ForegroundColor Yellow
Write-Host "Missing  : $Missing" -ForegroundColor Yellow
Write-Host "Warnings : $Warnings" -ForegroundColor Yellow
if ($Blockers -gt 0 -or $Missing -gt 0) { throw "Pack3B has server deployment blockers or missing required assets." }
