<#
.SYNOPSIS
    Complete-M1-12.v4.ps1 - archives the CORRECT roots: the whole emulation
    fixture tree (all six sources) plus the fleet dataset folder, then copies
    everything to OneDrive with hash verification.

.DESCRIPTION
    v3 defect fixed: it selected the first folder containing any SQL
    (pkl-mssql\init, 2 files) instead of the parent. v4 archives:
      1. deploy\fixtures\demo   - the six source-container init trees
         (also reports whether git already tracks them)
      2. C:\Workspace\PlantProcess-IQ_Archive\ppiq-fleet-3months - the fleet
         dataset + FLEET_RELATIONS ground truth
      3. the newest PPIQ_main_*.bundle
    Default target: OneDrive\PPIQ-Archive (exists on this machine per the
    v3 run). FLEET_RELATIONS.md is already committed (2b8a9a44) - verified.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-12.v4.ps1
#>

[CmdletBinding()]
param(
    [string]$RepoRoot      = (Get-Location).Path,
    [string]$FixturesRoot  = '',
    [string]$FleetRoot     = 'C:\Workspace\PlantProcess-IQ_Archive\ppiq-fleet-3months',
    [string]$ArchiveTarget = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("M1-12_Complete_v4_" + $stamp + ".txt")
$lines   = New-Object System.Collections.Generic.List[string]
$utf8    = New-Object System.Text.UTF8Encoding($false)
$fail    = 0

function W([string]$t = '') {
    $lines.Add($t)
    Write-Host $t
}
function Save {
    [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n") + "`r`n"), $utf8)
    Write-Host ''
    Write-Host ('Log: ' + $logPath) -ForegroundColor Cyan
}
function G {
    param([string[]]$GitArgs)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $out = & git @GitArgs 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $prev
    return @{ Code = $code; Out = @($out | ForEach-Object { "$_" }) }
}
function Zip-WithManifest([string]$srcRoot, [string]$label, [string]$workDir) {
    $assets = @(Get-ChildItem -LiteralPath $srcRoot -Recurse -File -Force -ErrorAction SilentlyContinue)
    if (@($assets).Count -eq 0) { return $null }
    $manifest = Join-Path $workDir ('MANIFEST_' + $label + '.sha256.txt')
    $mLines = New-Object System.Collections.Generic.List[string]
    $mLines.Add('# ' + $label + ' - ' + $stamp + ' - root: ' + $srcRoot)
    foreach ($a in $assets) {
        $h = (Get-FileHash -Algorithm SHA256 -LiteralPath $a.FullName).Hash
        $mLines.Add($h + '  ' + $a.FullName.Substring($srcRoot.Length).TrimStart('\', '/'))
    }
    [System.IO.File]::WriteAllText($manifest, (($mLines -join "`r`n") + "`r`n"), $utf8)
    $zip = Join-Path $workDir ('PPIQ_' + $label + '_' + $stamp + '.zip')
    Compress-Archive -Path (Join-Path $srcRoot '*') -DestinationPath $zip -CompressionLevel Optimal
    return @{ Zip = $zip; Manifest = $manifest; Count = @($assets).Count;
              Mb = [math]::Round((($assets | Measure-Object Length -Sum).Sum / 1MB), 1) }
}

W '=============================================================================='
W ('M1-12 CONTINUITY v4 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''

Set-Location $RepoRoot
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) { W 'FAIL: not a repo root.'; Save; exit 2 }

# ---- [1] verify the FLEET_RELATIONS commit ------------------------------------

W '[1] FLEET_RELATIONS'
$r = G @('ls-files', 'docs/emulation/')
$tracked = @($r.Out | Where-Object { $_ -match 'FLEET_RELATIONS' })
if (@($tracked).Count -gt 0) { W ('    committed: ' + ($tracked -join ', ') + '   (v3 did this: 2b8a9a44)') }
else { W '    MISSING from the index - v3 commit lost?'; $fail++ }
W ''

# ---- [2] the two roots ---------------------------------------------------------

W '[2] ARCHIVE ROOTS'
if ([string]::IsNullOrWhiteSpace($FixturesRoot)) {
    $FixturesRoot = Join-Path $RepoRoot 'deploy\fixtures\demo'
}
$roots = New-Object System.Collections.Generic.List[object]
if (Test-Path -LiteralPath $FixturesRoot) {
    $n = @(Get-ChildItem -LiteralPath $FixturesRoot -Recurse -File -ErrorAction SilentlyContinue).Count
    W ('    fixtures: ' + $FixturesRoot + '   (' + $n + ' files, all six sources)')
    $g = G @('ls-files', '--', 'deploy/fixtures/demo')
    $trackedCount = @($g.Out | Where-Object { $_ -ne '' }).Count
    if ($trackedCount -gt 0) {
        W ('    git already tracks ' + $trackedCount + ' of them - the bundle carries these too (double continuity)')
    } else {
        W '    git tracks NONE of them - the zip below is currently their ONLY durable copy.'
        W '    Consider committing deploy\fixtures\demo after the meeting (v23 decision:'
        W '    emulation harness in-repo vs separate emulation repo).'
    }
    $roots.Add(@{ Path = $FixturesRoot; Label = 'fixtures' })
} else {
    W ('    fixtures root missing: ' + $FixturesRoot); $fail++
}
if (Test-Path -LiteralPath $FleetRoot) {
    $n = @(Get-ChildItem -LiteralPath $FleetRoot -Recurse -File -ErrorAction SilentlyContinue).Count
    W ('    fleet:    ' + $FleetRoot + '   (' + $n + ' files)')
    $roots.Add(@{ Path = $FleetRoot; Label = 'fleet3months' })
} else {
    W ('    fleet root missing: ' + $FleetRoot + ' - pass -FleetRoot if it moved'); $fail++
}
if ($roots.Count -eq 0) { Save; exit 1 }
W ''

# ---- [3] zip both --------------------------------------------------------------

W '[3] ARCHIVE'
$workDir = Join-Path $RepoRoot ('.ppiq-continuity-' + $stamp)
New-Item -ItemType Directory -Path $workDir -Force | Out-Null
$artifacts = New-Object System.Collections.Generic.List[string]
foreach ($rt in $roots) {
    $z = Zip-WithManifest $rt.Path $rt.Label $workDir
    if ($null -eq $z) { W ('    ' + $rt.Label + ': no files - skipped'); continue }
    W ('    ' + $rt.Label.PadRight(14) + $z.Count.ToString().PadLeft(5) + ' files  ' + ([string]$z.Mb).PadLeft(8) + ' MB  -> ' + (Split-Path -Leaf $z.Zip))
    $artifacts.Add($z.Zip)
    $artifacts.Add($z.Manifest)
}
W ''

# ---- [4] off-machine copy -------------------------------------------------------

W '[4] OFF-MACHINE COPY'
if ([string]::IsNullOrWhiteSpace($ArchiveTarget)) {
    if ($env:OneDrive -and (Test-Path $env:OneDrive)) {
        $ArchiveTarget = Join-Path $env:OneDrive 'PPIQ-Archive'
        W ('    defaulting to ' + $ArchiveTarget)
    } else {
        W '    no target and no OneDrive - artifacts remain local.'; $fail++
    }
}
if (-not [string]::IsNullOrWhiteSpace($ArchiveTarget)) {
    if (-not (Test-Path $ArchiveTarget)) { New-Item -ItemType Directory -Path $ArchiveTarget -Force | Out-Null }
    $parent = Split-Path -Parent $RepoRoot
    $bundle = Get-ChildItem -Path $parent -Filter 'PPIQ_main_*.bundle' -ErrorAction SilentlyContinue |
              Sort-Object Name -Descending | Select-Object -First 1
    if ($bundle) { $artifacts.Add($bundle.FullName) }
    foreach ($srcItem in $artifacts) {
        $sha1 = (Get-FileHash -Algorithm SHA256 -LiteralPath $srcItem).Hash
        $dest = Join-Path $ArchiveTarget (Split-Path -Leaf $srcItem)
        Copy-Item -LiteralPath $srcItem -Destination $dest -Force
        $sha2 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dest).Hash
        if ($sha2 -eq $sha1) { W ('    copied + hash-verified   ' + $dest) }
        else { W ('    HASH MISMATCH            ' + $dest); $fail++ }
    }
    W ''
    W '    NOTE: OneDrive counts as off-machine once SYNC COMPLETES - check the'
    W '    tray icon shows the green check before calling this closed.'
}
W ''

W '=============================================================================='
if ($fail -eq 0) { W 'M1-12: COMPLETE. Fixtures + fleet + history are archived off-machine.' }
else { W ('M1-12: ' + $fail + ' item(s) open.') }
W '=============================================================================='
Save
if ($fail -eq 0) { exit 0 } else { exit 1 }
