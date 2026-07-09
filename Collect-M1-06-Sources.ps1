# ============================================================================
# Collect-M1-06-Sources.ps1
# READ-ONLY. Changes nothing.
#
# M1-06 Option A: move data-integration out of Admin.
#   New top-level "Data Integration" area: Connections/DB Links, Table Registry,
#   Importing, Jobs Monitor, Connector Truth.
#   Administrator keeps: Users/Roles, License, Site Identity, System health.
#   Old routes redirect. Action matrix + e2e updated.
#
# /admin/* is a WILDCARD route, so those five things are almost certainly TABS
# inside one page, not routes. This bundle captures the tab structure, the shared
# state between tabs, the nav definition, and the route table - everything needed
# to design the extraction before touching navigation.
#
# It also captures pages\PlatformOps, because AdminPageContent.tsx renders
# DemoAnalyticsWorkflowTruthPage - a demo page wired into the Administrator
# screen. That is in M1-06's blast radius whether or not we planned it.
#
# RUN: powershell -ExecutionPolicy Bypass -File .\Collect-M1-06-Sources.ps1
# Then upload the single file it prints.
# ============================================================================

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$RepoRoot = (Get-Location).Path
$SrcRoot  = Join-Path $RepoRoot 'Frontend\PlantProcess.Web\src'
if (-not (Test-Path $SrcRoot)) { Write-Host 'FATAL: run from the repo root.' -ForegroundColor Red; exit 1 }

$Stamp = Get-Date -Format 'ddMMMyyyy_HHmmss'
$Out   = Join-Path $RepoRoot ('M1-06_Sources_' + $Stamp + '.txt')

$sb = New-Object System.Text.StringBuilder
function W { param([string]$s) [void]$sb.AppendLine($s) }

W 'PPIQ M1-06 source bundle (Data Integration IA restructure)'
W ('Generated: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ''

# ---------------------------------------------------------------------------
# 1. Directory maps first - cheap orientation before the code
# ---------------------------------------------------------------------------
W '=========================================================='
W '1. DIRECTORY MAP'
W '=========================================================='
foreach ($d in @('pages\Admin', 'pages\PlatformOps')) {
    $full = Join-Path $SrcRoot $d
    W ''
    W ('--- src\' + $d)
    if (-not (Test-Path $full)) { W '    (does not exist)'; continue }
    Get-ChildItem $full -Recurse -File | Sort-Object FullName | ForEach-Object {
        W ('    ' + $_.FullName.Substring($SrcRoot.Length + 1) + '   [' + $_.Length + ' bytes]')
    }
}

# ---------------------------------------------------------------------------
# 2. The admin route + nav wiring, without dumping all of App.tsx
# ---------------------------------------------------------------------------
W ''
W '=========================================================='
W '2. ADMIN ROUTE + NAV WIRING (grep, not full files)'
W '=========================================================='
$appPath = Join-Path $SrcRoot 'App.tsx'
W ''
W '--- App.tsx lines mentioning admin / data-integration / import / jobs'
if (Test-Path $appPath) {
    $lines = [System.IO.File]::ReadAllLines($appPath)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match 'admin|data-integration|Import|JobsMonitor|jobs|connector|registry') {
            W ('    ' + ($i + 1) + ': ' + $lines[$i].Trim())
        }
    }
}

# ---------------------------------------------------------------------------
# 3. Full files that must be read, not grepped
# ---------------------------------------------------------------------------
W ''
W '=========================================================='
W '3. FULL FILES'
W '=========================================================='

$Wanted = @()
foreach ($d in @('pages\Admin')) {
    $full = Join-Path $SrcRoot $d
    if (Test-Path $full) {
        Get-ChildItem $full -Recurse -File |
            Where-Object { $_.Extension -in '.ts', '.tsx' } |
            Where-Object { $_.Name -notmatch '\.test\.|\.stories\.' } |
            ForEach-Object { $Wanted += $_.FullName }
    }
}
$Wanted += (Join-Path $SrcRoot 'components\AppLayout.tsx')
$Wanted += (Join-Path $SrcRoot 'pages\PlatformOps\DemoAnalyticsPages.implementation.tsx')

$included = 0
foreach ($full in ($Wanted | Sort-Object -Unique)) {
    if (-not (Test-Path $full)) { W ('!!! NOT ON DISK: ' + $full); continue }
    $rel  = $full.Substring($RepoRoot.Length + 1)
    $text = [System.IO.File]::ReadAllText($full)
    $included++
    W ''
    W ('==================== FILE: ' + $rel + ' (' + ($text -split "`n").Count + ' lines) ====================')
    W $text
    W ('==================== END: ' + $rel + ' ====================')
}

# ---------------------------------------------------------------------------
# 4. Which components does the Admin page compose? (import graph, one hop)
# ---------------------------------------------------------------------------
W ''
W '=========================================================='
W '4. WHAT ADMIN IMPORTS (one hop out - shows the real tab components)'
W '=========================================================='
$adminDir = Join-Path $SrcRoot 'pages\Admin'
if (Test-Path $adminDir) {
    Get-ChildItem $adminDir -Recurse -File | Where-Object { $_.Extension -eq '.tsx' } | ForEach-Object {
        $rel = $_.FullName.Substring($SrcRoot.Length + 1)
        W ''
        W ('--- ' + $rel)
        [System.IO.File]::ReadAllLines($_.FullName) | Where-Object { $_ -match '^\s*import ' } | ForEach-Object { W ('    ' + $_.Trim()) }
    }
}

# ---------------------------------------------------------------------------
# 5. e2e / action-matrix references to admin routes (M1-06 must update these)
# ---------------------------------------------------------------------------
W ''
W '=========================================================='
W '5. TESTS / MATRICES REFERENCING /admin (M1-06 must update these)'
W '=========================================================='
$all = Get-ChildItem $SrcRoot -Recurse -File |
    Where-Object { $_.Extension -in '.ts', '.tsx' } |
    Where-Object { $_.FullName -notmatch '_phase9_standardbutton_dedupe_backup' }
foreach ($f in $all) {
    $lines = [System.IO.File]::ReadAllLines($f.FullName)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '"/admin' -or $lines[$i] -match "'/admin") {
            W ('    ' + $f.FullName.Substring($SrcRoot.Length + 1) + ':' + ($i + 1) + '  ' + $lines[$i].Trim())
        }
    }
}
# playwright specs live outside src
$e2e = Join-Path $RepoRoot 'Frontend\PlantProcess.Web\e2e'
if (Test-Path $e2e) {
    W ''
    W '--- e2e specs mentioning /admin'
    Get-ChildItem $e2e -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        $lines = [System.IO.File]::ReadAllLines($_.FullName)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '/admin') { W ('    ' + $_.Name + ':' + ($i + 1) + '  ' + $lines[$i].Trim()) }
        }
    }
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($Out, $sb.ToString(), $utf8)

Write-Host ''
Write-Host ('Bundle written: ' + $Out) -ForegroundColor Green
Write-Host ('Full files included: ' + $included)
Write-Host ('Size: ' + [math]::Round((Get-Item $Out).Length / 1KB, 1) + ' KB')
Write-Host 'Nothing on disk was modified. Upload that file.'
