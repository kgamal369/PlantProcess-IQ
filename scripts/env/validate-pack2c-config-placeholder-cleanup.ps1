$ErrorActionPreference = "Stop"
$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$ConfigDocRoot = "$RepoRoot\Documentation\config"
$Latest = Get-ChildItem "$ConfigDocRoot\Pack2C_PostCleanupSecretScan_*.csv" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $Latest) { throw "No Pack 2C post-cleanup scan CSV found." }
$Rows = Import-Csv $Latest.FullName
$High = @($Rows | Where-Object { $_.Risk -eq "HIGH" }).Count
$Placeholder = @($Rows | Where-Object { $_.Risk -eq "OK_PLACEHOLDER" }).Count
$Low = @($Rows | Where-Object { $_.Risk -eq "LOW" }).Count
$JsonFiles = @(
    "$RepoRoot\Backend\PlantProcess.Api\appsettings.json",
    "$RepoRoot\Backend\PlantProcess.Api\appsettings.Development.json",
    "$RepoRoot\Backend\PlantProcess.Api\Properties\launchSettings.json"
)
foreach ($Json in $JsonFiles) {
    if (Test-Path $Json) { Get-Content $Json -Raw | ConvertFrom-Json | Out-Null }
}
Write-Host "[GREEN] Pack 2C config placeholder cleanup validation executed." -ForegroundColor Green
Write-Host "HIGH findings       : $High" -ForegroundColor Yellow
Write-Host "Placeholder findings: $Placeholder" -ForegroundColor Yellow
Write-Host "Low info findings   : $Low" -ForegroundColor Yellow
if ($High -gt 0) { throw "Pack 2C still has HIGH tracked config secret findings." }
