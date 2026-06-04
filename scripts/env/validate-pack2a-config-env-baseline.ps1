$ErrorActionPreference = "Stop"
$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$ConfigDocRoot = "$RepoRoot\Documentation\config"
$Required = @(
    "Environment_Contract.md",
    "Local_Server_Customer_Profile_Matrix.md",
    "Secret_Config_Scan.md"
)
foreach ($Item in $Required) {
    $Path = Join-Path $ConfigDocRoot $Item
    if (-not (Test-Path $Path)) { throw "Missing config documentation file: $Item" }
}
$LatestGate = Get-ChildItem "$ConfigDocRoot\Pack2A_ConfigEnvGate_*.csv" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $LatestGate) { throw "Missing Pack2A gate CSV." }
$GateRows = Import-Csv $LatestGate.FullName
$Missing = @($GateRows | Where-Object { $_.Status -eq "MISSING" }).Count
$Warn = @($GateRows | Where-Object { $_.Status -eq "WARN" }).Count
Write-Host "[GREEN] Pack 2A config/env baseline validation executed." -ForegroundColor Green
Write-Host "Gate rows : $(@($GateRows).Count)" -ForegroundColor Green
Write-Host "Missing   : $Missing" -ForegroundColor Yellow
Write-Host "Warnings  : $Warn" -ForegroundColor Yellow
if ($Missing -gt 0) { throw "Pack2A has missing required standardization files." }
