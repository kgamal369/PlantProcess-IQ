<#
.SYNOPSIS
    Complete-M1-12.ps1 - continuity: proves FLEET_RELATIONS is committed, archives
    the emulation assets (SQL seeds, compose files, fixtures) with a SHA256
    manifest, and copies archive + repo bundle off this machine.

.DESCRIPTION
    The constitution (Emulation Doctrine): emulation assets are versioned,
    reproducible, and stored durably - NEVER on one laptop. Remaining board items:
      1. verify FLEET_RELATIONS.md committed   -> done here (commits it if found
                                                  uncommitted at a known location)
      2. archive SQLs                          -> done here (zip + per-file SHA256)
      3. bundle off-machine                    -> done here (uses the newest
                                                  PPIQ_main_*.bundle, or creates one)

    -ArchiveTarget names the off-machine destination (USB, network share, synced
    cloud folder). Without it the archive is still built locally and the script
    exits 1, because "archived onto the same laptop" is not continuity.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-12.ps1 -ArchiveTarget "E:\PPIQ-Archive"

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-12.ps1 -EmulationRoot "C:\Workspace\ppiq-fleet-3months" -ArchiveTarget "\\nas\ppiq"
#>

[CmdletBinding()]
param(
    [string]$RepoRoot      = (Get-Location).Path,
    [string]$EmulationRoot = '',
    [string]$ArchiveTarget = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("M1-12_Complete_" + $stamp + ".txt")
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
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$GitArgs)
    $out = & git @GitArgs 2>&1
    return @{ Code = $LASTEXITCODE; Out = @($out | ForEach-Object { "$_" }) }
}

W '=============================================================================='
W ('M1-12 CONTINUITY - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('Repo: ' + $RepoRoot)
W '=============================================================================='
W ''

Set-Location $RepoRoot
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) {
    W 'FAIL: not a git repository root.'
    Save; exit 2
}

# ---- [1] FLEET_RELATIONS committed? ----------------------------------------

W '[1] FLEET_RELATIONS'
$r = G ls-files 'docs/emulation/'
$tracked = @($r.Out | Where-Object { $_ -match 'FLEET_RELATIONS' })
if (@($tracked).Count -gt 0) {
    foreach ($t in $tracked) {
        $r2 = G log -1 --format='%h %ad' --date=short -- $t
        W ('    committed: ' + $t + '   (' + ($r2.Out -join ' ') + ')')
    }
} else {
    W '    NOT tracked under docs/emulation/. Searching known rescue locations...'
    $candidates = @()
    foreach ($probe in @(
        (Join-Path $RepoRoot 'docs\emulation\FLEET_RELATIONS.md'),
        (Join-Path $RepoRoot 'FLEET_RELATIONS.md'),
        (Join-Path $RepoRoot 'docs\FLEET_RELATIONS.md')
    )) {
        if (Test-Path $probe) { $candidates += $probe }
    }
    if ([string]::IsNullOrWhiteSpace($EmulationRoot) -eq $false -and (Test-Path $EmulationRoot)) {
        $hit = Get-ChildItem -Path $EmulationRoot -Filter 'FLEET_RELATIONS*.md' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit) { $candidates += $hit.FullName }
    }
    if (@($candidates).Count -eq 0) {
        W '    FAIL: FLEET_RELATIONS.md not found anywhere probed. The money-slide'
        W '    provenance (R1 9.3x, seed=42, R2-R17) lives in that file. Locate it'
        W '    and re-run with it placed at docs\emulation\FLEET_RELATIONS.md.'
        $fail++
    } else {
        $src = $candidates[0]
        $dstDir = Join-Path $RepoRoot 'docs\emulation'
        if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
        $dst = Join-Path $dstDir 'FLEET_RELATIONS.md'
        if ($src -ne $dst) { Copy-Item -LiteralPath $src -Destination $dst -Force }
        $null = G add -- 'docs/emulation/FLEET_RELATIONS.md'
        $r3 = G commit -m 'M1-12: commit FLEET_RELATIONS emulation provenance (planted relations R1-R17, seed=42)'
        if ($r3.Code -eq 0) {
            W ('    rescued from ' + $src)
            W ('    committed as docs/emulation/FLEET_RELATIONS.md  ' + ((G rev-parse HEAD).Out[0]).Substring(0, 12))
        } else {
            W ('    COMMIT FAILED: ' + ($r3.Out -join ' ')); $fail++
        }
    }
}
W ''

# ---- [2] locate emulation assets -------------------------------------------

W '[2] EMULATION ASSETS'
if ([string]::IsNullOrWhiteSpace($EmulationRoot)) {
    foreach ($probe in @(
        'C:\Workspace\ppiq-fleet-3months',
        (Join-Path (Split-Path -Parent $RepoRoot) 'ppiq-fleet-3months'),
        (Join-Path $RepoRoot 'source-systems')
    )) {
        if (Test-Path $probe) { $EmulationRoot = $probe; break }
    }
}
if ([string]::IsNullOrWhiteSpace($EmulationRoot) -or -not (Test-Path $EmulationRoot)) {
    W '    FAIL: emulation root not found. Pass -EmulationRoot pointing at the'
    W '    fleet dataset folder (compose files + seed SQLs). The Emulation Doctrine'
    W '    forbids these living on one laptop; they cannot be archived if unfound.'
    $fail++
    Save; exit 1
}
W ('    root: ' + $EmulationRoot)

$patterns = @('*.sql', '*.yml', '*.yaml', '*.csv', '*.md', '*.ps1', '*.sh', 'Dockerfile*')
$assets = @()
foreach ($p in $patterns) {
    $assets += Get-ChildItem -Path $EmulationRoot -Filter $p -Recurse -File -ErrorAction SilentlyContinue
}
$assets = @($assets | Sort-Object FullName -Unique)
if (@($assets).Count -eq 0) {
    W '    FAIL: no archivable assets under the root (no sql/yml/csv/md/scripts).'
    $fail++
    Save; exit 1
}
$totalMb = [math]::Round((($assets | Measure-Object Length -Sum).Sum / 1MB), 1)
W ('    files: ' + @($assets).Count + '   total ' + $totalMb + ' MB')

# ---- [3] archive with manifest ---------------------------------------------

W ''
W '[3] ARCHIVE'
$workDir   = Join-Path $RepoRoot ('.ppiq-continuity-' + $stamp)
New-Item -ItemType Directory -Path $workDir -Force | Out-Null
$manifest  = Join-Path $workDir 'MANIFEST.sha256.txt'
$mLines    = New-Object System.Collections.Generic.List[string]
$mLines.Add('# PPIQ emulation continuity archive - ' + $stamp)
$mLines.Add('# root: ' + $EmulationRoot)
foreach ($a in $assets) {
    $h = (Get-FileHash -Algorithm SHA256 -LiteralPath $a.FullName).Hash
    $rel = $a.FullName.Substring($EmulationRoot.Length).TrimStart('\', '/')
    $mLines.Add($h + '  ' + $rel)
}
[System.IO.File]::WriteAllText($manifest, (($mLines -join "`r`n") + "`r`n"), $utf8)

$zipName = 'PPIQ_emulation_' + $stamp + '.zip'
$zipPath = Join-Path $workDir $zipName
Compress-Archive -Path (Join-Path $EmulationRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
$zipSha  = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash
$zipMb   = [math]::Round(((Get-Item -LiteralPath $zipPath).Length / 1MB), 1)
W ('    zip       ' + $zipPath + '   (' + $zipMb + ' MB)')
W ('    sha256    ' + $zipSha)
W ('    manifest  ' + $manifest + '   (' + (@($assets).Count) + ' entries)')

# ---- [4] repo bundle ---------------------------------------------------------

W ''
W '[4] REPO BUNDLE'
$parent = Split-Path -Parent $RepoRoot
$bundle = Get-ChildItem -Path $parent -Filter 'PPIQ_main_*.bundle' -ErrorAction SilentlyContinue |
          Sort-Object Name -Descending | Select-Object -First 1
if ($bundle) {
    W ('    using existing ' + $bundle.FullName)
    $bundlePath = $bundle.FullName
} else {
    $bundlePath = Join-Path $parent ('PPIQ_main_' + $stamp + '.bundle')
    $r = G bundle create $bundlePath --all
    if ($r.Code -ne 0) {
        W ('    bundle create FAILED: ' + ($r.Out -join ' ')); $fail++
        $bundlePath = ''
    } else {
        W ('    created ' + $bundlePath)
    }
}
if ($bundlePath -ne '') {
    $r = G bundle verify $bundlePath
    if ($r.Code -ne 0) { W '    bundle VERIFY FAILED'; $fail++ }
    else { W '    bundle verified OK' }
}

# ---- [5] off-machine copy ----------------------------------------------------

W ''
W '[5] OFF-MACHINE COPY'
if ([string]::IsNullOrWhiteSpace($ArchiveTarget)) {
    W '    NO -ArchiveTarget GIVEN. The archive exists but it is ON THIS LAPTOP,'
    W '    which is exactly the state M1-12 exists to end. Re-run with'
    W '    -ArchiveTarget "E:\PPIQ-Archive" (USB) or a synced/network folder.'
    $fail++
} else {
    if (-not (Test-Path $ArchiveTarget)) { New-Item -ItemType Directory -Path $ArchiveTarget -Force | Out-Null }
    $pairs = @(
        @{ Src = $zipPath;  Sha = $zipSha }
        @{ Src = $manifest; Sha = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifest).Hash }
    )
    if ($bundlePath -ne '') {
        $pairs += @{ Src = $bundlePath; Sha = (Get-FileHash -Algorithm SHA256 -LiteralPath $bundlePath).Hash }
    }
    foreach ($p in $pairs) {
        $dest = Join-Path $ArchiveTarget (Split-Path -Leaf $p.Src)
        Copy-Item -LiteralPath $p.Src -Destination $dest -Force
        $sha2 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dest).Hash
        if ($sha2 -eq $p.Sha) {
            W ('    copied + hash-verified   ' + $dest)
        } else {
            W ('    HASH MISMATCH            ' + $dest); $fail++
        }
    }
}
W ''

# ---- verdict ----------------------------------------------------------------

W '=============================================================================='
if ($fail -eq 0) {
    W 'M1-12: COMPLETE. Emulation assets + repo history exist off this machine,'
    W 'hash-verified. Keep this log with the archive.'
} else {
    W ('M1-12: ' + $fail + ' item(s) OPEN - see above. Continuity is not yet real.')
}
W '=============================================================================='
Save
if ($fail -eq 0) { exit 0 } else { exit 1 }
