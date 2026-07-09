# ============================================================================
# Collect-Step3-Sources.ps1
# READ-ONLY. Changes nothing. Bundles exactly the files Step 3 needs into a
# single .txt so one upload replaces ten.
#
# RUN: powershell -ExecutionPolicy Bypass -File .\Collect-Step3-Sources.ps1
# Then upload the file it prints at the end.
# ============================================================================

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$RepoRoot = (Get-Location).Path
$SrcRoot  = Join-Path $RepoRoot 'Frontend\PlantProcess.Web\src'
if (-not (Test-Path $SrcRoot)) { Write-Host 'FATAL: run from the repo root.' -ForegroundColor Red; exit 1 }

$Stamp = Get-Date -Format 'ddMMMyyyy_HHmmss'
$Out   = Join-Path $RepoRoot ('Step3_Sources_' + $Stamp + '.txt')

# Explicit list first, then a directory sweep for the standard primitives.
$Wanted = @(
    'pages\Advisory\HonestyCertificationPage.tsx',
    'pages\Advisory\BenchmarkingPage.tsx',
    'pages\Advisory\RoiCfoDashboardPage.tsx',
    'pages\Advisory\ValueRealizationPage.tsx',
    'pages\Advisory\RecommendationsPage.tsx',
    'pages\Advisory\ScenarioSimulationPage.tsx',
    'pages\EdgeCollector\EdgeCollectorPage.tsx',
    'pages\HistorianConnector\HistorianConnectorPage.tsx',
    'api\advisoryApi.ts',
    'components\AppLayout.css',
    'state\AuthContext.tsx'
)

$files = @()
foreach ($rel in $Wanted) {
    $full = Join-Path $SrcRoot $rel
    if (Test-Path $full) { $files += (Get-Item $full) }
    else { Write-Host ('  MISSING (will be noted in the bundle): ' + $rel) -ForegroundColor Yellow }
}

# All standard primitives + the routes file.
Get-ChildItem (Join-Path $SrcRoot 'components\standard') -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in '.ts', '.tsx', '.css' } | ForEach-Object { $files += $_ }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('PPIQ Step 3 source bundle')
[void]$sb.AppendLine('Generated: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
[void]$sb.AppendLine('Files: ' + $files.Count)
[void]$sb.AppendLine('')

foreach ($rel in $Wanted) {
    if (-not (Test-Path (Join-Path $SrcRoot $rel))) {
        [void]$sb.AppendLine('!!! MISSING FROM DISK: src\' + $rel)
    }
}
[void]$sb.AppendLine('')

foreach ($f in ($files | Sort-Object FullName -Unique)) {
    $rel = $f.FullName.Substring($RepoRoot.Length + 1)
    $text = [System.IO.File]::ReadAllText($f.FullName)
    [void]$sb.AppendLine('==================== FILE: ' + $rel + ' (' + ($text -split "`n").Count + ' lines) ====================')
    [void]$sb.AppendLine($text)
    [void]$sb.AppendLine('==================== END: ' + $rel + ' ====================')
    [void]$sb.AppendLine('')
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($Out, $sb.ToString(), $utf8)

Write-Host ''
Write-Host ('Bundle written: ' + $Out) -ForegroundColor Green
Write-Host ('Files included: ' + ($files | Sort-Object FullName -Unique).Count)
Write-Host ('Size: ' + [math]::Round((Get-Item $Out).Length / 1KB, 1) + ' KB')
Write-Host ''
Write-Host 'Upload that single file. Nothing on disk was modified.'
