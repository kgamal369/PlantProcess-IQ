# ============================================================================
# Collect-Step3b-Sources.ps1
# READ-ONLY. Changes nothing. Bundles the files Step 3b needs.
#
# Step 3b will:
#   * Recommendations : "Reload demo request" -> "Load sample request",
#                       demo-approver -> logged-in user (useAuth),
#                       "Dismissed from demo workspace." -> "Recommendation dismissed."
#   * Value Realisation: "Reload demo request" -> "Load sample request",
#                       "Loading demo request..." -> honest wording,
#                       a visible "Sample data" badge on the panel
#   * App.tsx         : remove the /license route + its two redirects + lazy import
#   * PersonaAccessMatrixPage : reword "Role matrix for demo and buyer review."
#   * the phase-token gate : drop the now-dead /phase10/license and /license-demo
#                       redirect assertions (a gate cannot outlive the route it guards)
#
# RUN: powershell -ExecutionPolicy Bypass -File .\Collect-Step3b-Sources.ps1
# Then upload the single file it prints.
# ============================================================================

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$RepoRoot = (Get-Location).Path
$SrcRoot  = Join-Path $RepoRoot 'Frontend\PlantProcess.Web\src'
if (-not (Test-Path $SrcRoot)) { Write-Host 'FATAL: run from the repo root.' -ForegroundColor Red; exit 1 }

$Stamp = Get-Date -Format 'ddMMMyyyy_HHmmss'
$Out   = Join-Path $RepoRoot ('Step3b_Sources_' + $Stamp + '.txt')

$Wanted = @(
    'App.tsx',
    'pages\Advisory\RecommendationsPage.tsx',
    'pages\Advisory\ValueRealizationPage.tsx',
    'pages\Phase9\PersonaAccessMatrixPage.tsx',
    'pages\Phase10\Phase10LicenseDemoPage.tsx',
    'test\architecture\noPhaseTokensOnDemoPath.test.ts',
    'state\AuthContext.tsx'
)

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('PPIQ Step 3b source bundle')
[void]$sb.AppendLine('Generated: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
[void]$sb.AppendLine('')

$found = 0
foreach ($rel in $Wanted) {
    $full = Join-Path $SrcRoot $rel
    if (-not (Test-Path $full)) {
        [void]$sb.AppendLine('!!! NOT ON DISK: src\' + $rel)
        Write-Host ('  NOT FOUND: ' + $rel) -ForegroundColor Yellow
        continue
    }
    $found++
    $text = [System.IO.File]::ReadAllText($full)
    [void]$sb.AppendLine('==================== FILE: src\' + $rel + ' (' + ($text -split "`n").Count + ' lines) ====================')
    [void]$sb.AppendLine($text)
    [void]$sb.AppendLine('==================== END: src\' + $rel + ' ====================')
    [void]$sb.AppendLine('')
}

# Where does /license actually come from? Report it rather than assume.
[void]$sb.AppendLine('---- grep: license route wiring in App.tsx ----')
$app = Join-Path $SrcRoot 'App.tsx'
if (Test-Path $app) {
    Select-String -Path $app -Pattern 'license', 'LicenseDemoPage' -SimpleMatch |
        ForEach-Object { [void]$sb.AppendLine($_.LineNumber.ToString() + ': ' + $_.Line.Trim()) }
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($Out, $sb.ToString(), $utf8)

Write-Host ''
Write-Host ('Bundle written: ' + $Out) -ForegroundColor Green
Write-Host ('Files included: ' + $found + ' of ' + $Wanted.Count)
Write-Host ('Size: ' + [math]::Round((Get-Item $Out).Length / 1KB, 1) + ' KB')
Write-Host 'Nothing on disk was modified.'
