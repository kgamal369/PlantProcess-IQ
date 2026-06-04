$ErrorActionPreference = "Stop"
$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$ConfigDocRoot = "$RepoRoot\Documentation\config"
$Latest = Get-ChildItem "$ConfigDocRoot\Pack2D_FinalEnvProfileGate_*.csv" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $Latest) { throw "No Pack2D final env profile gate CSV found." }
$Rows = Import-Csv $Latest.FullName
$Blockers = @($Rows | Where-Object { $_.Status -eq "BLOCKER" }).Count
$Missing = @($Rows | Where-Object { $_.Status -eq "MISSING" }).Count
$Warn = @($Rows | Where-Object { $_.Status -eq "WARN" }).Count
Write-Host "[GREEN] Pack 2D final env/profile standardization validation executed." -ForegroundColor Green
Write-Host "Gate rows: $(@($Rows).Count)" -ForegroundColor Green
Write-Host "Blockers : $Blockers" -ForegroundColor Yellow
Write-Host "Missing  : $Missing" -ForegroundColor Yellow
Write-Host "Warnings : $Warn" -ForegroundColor Yellow
if ($Blockers -gt 0 -or $Missing -gt 0) { throw "Pack2D has blockers or missing gates." }
