param(
    [string]$Project = "chromium",
    [switch]$ListOnly
)

$ErrorActionPreference = "Stop"
$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$WebRoot = "$RepoRoot\Frontend\PlantProcess.Web"
$TestingRoot = "$RepoRoot\Documentation\testing"
$Latest = Get-ChildItem "$TestingRoot\S3C_StableE2ECoreManifest_*.csv" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $Latest) { throw "No S3C stable E2E manifest found." }
$Rows = Import-Csv $Latest.FullName
$SpecArgs = New-Object System.Collections.Generic.List[string]
foreach ($R in $Rows) {
    $FullPath = Join-Path $RepoRoot $R.RelativePath
    if (Test-Path $FullPath) {
        $RelToWeb = $R.RelativePath.Substring("Frontend\PlantProcess.Web\".Length).Replace("\","/")
        $SpecArgs.Add($RelToWeb) | Out-Null
    }
}
if ($SpecArgs.Count -lt 1) { throw "Stable E2E manifest has no existing spec files." }
Write-Host "Stable E2E specs:" -ForegroundColor Cyan
foreach ($Spec in $SpecArgs) { Write-Host " - $Spec" -ForegroundColor Yellow }
if ($ListOnly) { exit 0 }
Set-Location $WebRoot
$ArgsList = @("playwright", "test") + $SpecArgs + @("--project=" + $Project, "--workers=1", "--reporter=line")
& npx @ArgsList
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "[GREEN] Stable E2E core passed." -ForegroundColor Green
