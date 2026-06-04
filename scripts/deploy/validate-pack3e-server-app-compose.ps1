$ErrorActionPreference = "Stop"
$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$DocRoot = "$RepoRoot\Documentation\deployment"
$Latest = Get-ChildItem "$DocRoot\Pack3E_ServerAppComposeGate_*.csv" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $Latest) { throw "No Pack3E server app compose gate CSV found." }
$Rows = Import-Csv $Latest.FullName
$Blockers = @($Rows | Where-Object { $_.Status -eq "BLOCKER" }).Count
$Missing = @($Rows | Where-Object { $_.Status -eq "MISSING" }).Count
$Warnings = @($Rows | Where-Object { $_.Status -eq "WARN" }).Count
Write-Host "[GREEN] Pack 3E server app compose validation executed." -ForegroundColor Green
Write-Host "Gate rows: $(@($Rows).Count)" -ForegroundColor Green
Write-Host "Blockers : $Blockers" -ForegroundColor Yellow
Write-Host "Missing  : $Missing" -ForegroundColor Yellow
Write-Host "Warnings : $Warnings" -ForegroundColor Yellow
if ($Blockers -gt 0 -or $Missing -gt 0) { throw "Pack3E has server app compose blockers or missing required assets." }
