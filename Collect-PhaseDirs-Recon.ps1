# ============================================================================
# Collect-PhaseDirs-Recon.ps1
# READ-ONLY. Changes nothing. Maps the remaining phase-named directories, who
# imports them, and where a rename would COLLIDE with an existing directory.
#
# Why: pages\Assistant already exists alongside pages\Phase8. Renaming Phase8 ->
# Assistant would merge two directories and could shadow a real module. This
# script produces the facts needed to design the rename safely.
#
# RUN: powershell -ExecutionPolicy Bypass -File .\Collect-PhaseDirs-Recon.ps1
# Then upload the single file it prints.
# ============================================================================

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$RepoRoot = (Get-Location).Path
$SrcRoot  = Join-Path $RepoRoot 'Frontend\PlantProcess.Web\src'
if (-not (Test-Path $SrcRoot)) { Write-Host 'FATAL: run from the repo root.' -ForegroundColor Red; exit 1 }

$Stamp = Get-Date -Format 'ddMMMyyyy_HHmmss'
$Out   = Join-Path $RepoRoot ('PhaseDirs_Recon_' + $Stamp + '.txt')

$sb = New-Object System.Text.StringBuilder
function W { param([string]$s) [void]$sb.AppendLine($s) }

W 'PPIQ phase-directory recon'
W ('Generated: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ''

# ---------------------------------------------------------------------------
# 1. Contents of every phase-named directory
# ---------------------------------------------------------------------------
$PhaseDirs = @(
    'pages\Phase7ValueScenario',
    'pages\Phase8',
    'pages\Phase9',
    'pages\Phase10',
    'phase11'
)

W '=========================================================='
W '1. CONTENTS OF PHASE-NAMED DIRECTORIES'
W '=========================================================='
foreach ($d in $PhaseDirs) {
    $full = Join-Path $SrcRoot $d
    W ''
    W ('--- src\' + $d)
    if (-not (Test-Path $full)) { W '    (does not exist)'; continue }
    Get-ChildItem $full -Recurse -File | ForEach-Object {
        W ('    ' + $_.FullName.Substring($SrcRoot.Length + 1) + '   [' + $_.Length + ' bytes]')
    }
}

# ---------------------------------------------------------------------------
# 2. Collision check: does a same-named target directory already exist?
# ---------------------------------------------------------------------------
W ''
W '=========================================================='
W '2. RENAME COLLISION CHECK (proposed target -> already exists?)'
W '=========================================================='
$Proposed = @{
    'pages\Phase7ValueScenario' = 'pages\ValueScenario'
    'pages\Phase8'              = 'pages\Assistant'
    'pages\Phase9'              = 'pages\Personas'
    'pages\Phase10'             = '(delete - orphan css only)'
    'phase11'                   = 'dashboard-widgets'
}
foreach ($k in $Proposed.Keys) {
    $target = $Proposed[$k]
    if ($target -like '(*') { W ('  ' + $k + '  ->  ' + $target); continue }
    $tPath = Join-Path $SrcRoot $target
    $exists = Test-Path $tPath
    W ('  ' + $k.PadRight(28) + ' -> ' + $target.PadRight(24) + ' EXISTS: ' + $exists)
    if ($exists) {
        Get-ChildItem $tPath -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            W ('       existing: ' + $_.Name)
        }
    }
}

# ---------------------------------------------------------------------------
# 3. Who imports these paths?
# ---------------------------------------------------------------------------
W ''
W '=========================================================='
W '3. IMPORTERS (every file referencing a phase-named path)'
W '=========================================================='
$patterns = @('pages/Phase7ValueScenario', 'pages/Phase8', 'pages/Phase9', 'pages/Phase10',
              'phase11', 'phase10-license', 'phase8-ai', 'phase9-')

$files = Get-ChildItem $SrcRoot -Recurse -File |
    Where-Object { $_.Extension -in '.ts', '.tsx', '.css' } |
    Where-Object { $_.FullName -notmatch '_phase9_standardbutton_dedupe_backup' }

foreach ($pat in $patterns) {
    W ''
    W ('--- references to "' + $pat + '"')
    $hits = 0
    foreach ($f in $files) {
        $lines = [System.IO.File]::ReadAllLines($f.FullName)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -like ('*' + $pat + '*')) {
                W ('    ' + $f.FullName.Substring($SrcRoot.Length + 1) + ':' + ($i + 1) + '  ' + $lines[$i].Trim())
                $hits++
            }
        }
    }
    if ($hits -eq 0) { W '    (no references - safe to delete/rename)' }
}

# ---------------------------------------------------------------------------
# 4. Is phase10-license.css imported by anything?  (it is the last Phase10 file)
# ---------------------------------------------------------------------------
W ''
W '=========================================================='
W '4. ORPHAN CHECK: phase10-license.css'
W '=========================================================='
$cssRefs = 0
foreach ($f in $files) {
    $t = [System.IO.File]::ReadAllText($f.FullName)
    if ($t -like '*phase10-license*') { W ('    referenced by: ' + $f.FullName.Substring($SrcRoot.Length + 1)); $cssRefs++ }
}
if ($cssRefs -eq 0) { W '    NO references. The file is orphaned by the /license removal and can be deleted.' }

# ---------------------------------------------------------------------------
# 5. phase-named className / data-testid still in source
# ---------------------------------------------------------------------------
W ''
W '=========================================================='
W '5. PHASE-NAMED className / data-testid (internal, not user-visible)'
W '=========================================================='
foreach ($f in $files) {
    $lines = [System.IO.File]::ReadAllLines($f.FullName)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '(className|data-testid)="[^"]*phase\d') {
            W ('    ' + $f.FullName.Substring($SrcRoot.Length + 1) + ':' + ($i + 1) + '  ' + $lines[$i].Trim())
        }
    }
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($Out, $sb.ToString(), $utf8)

Write-Host ''
Write-Host ('Recon written: ' + $Out) -ForegroundColor Green
Write-Host ('Size: ' + [math]::Round((Get-Item $Out).Length / 1KB, 1) + ' KB')
Write-Host 'Nothing on disk was modified. Upload that file.'
