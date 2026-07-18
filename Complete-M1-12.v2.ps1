<#
.SYNOPSIS
    Complete-M1-12.v2.ps1 - continuity, with a discovery pass. Finds the
    emulation assets before archiving them, instead of failing on guessed paths.

.DESCRIPTION
    v1 probed three fixed paths and failed. v2 discovers:
      [0a] Docker itself - every running/stopped container carries the compose
           label com.docker.compose.project.working_dir, which is exactly where
           the fleet compose file and its seed SQLs live. This is authoritative:
           the containers that served today's imports came from somewhere.
      [0b] Filesystem sweep for FLEET_RELATIONS*.md and fleet-shaped folders
           across C:\Workspace, the user profile, and fixed drives (shallow).

    Then the v1 flow: commit FLEET_RELATIONS under docs/emulation/, zip the
    assets with a SHA256 manifest, reuse or create the repo bundle, copy
    everything to -ArchiveTarget with hash verification.

    If discovery finds NOTHING, the fleet assets may exist only INSIDE docker
    volumes - the script then extracts a pg_dump/mysqldump fallback offer as
    instructions, because continuity of the money slide cannot wait on a lost
    folder.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-12.v2.ps1 -ArchiveTarget "E:\PPIQ-Archive"

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-12.v2.ps1 -EmulationRoot "D:\ppiq-fleet-3months" -ArchiveTarget "E:\PPIQ-Archive"
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
$logPath = Join-Path $RepoRoot ("M1-12_Complete_v2_" + $stamp + ".txt")
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
function D {
    param([string[]]$DockerArgs)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $out = & docker @DockerArgs 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $prev
    return @{ Code = $code; Out = @($out | ForEach-Object { "$_" }) }
}

W '=============================================================================='
W ('M1-12 CONTINUITY v2 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('Repo: ' + $RepoRoot)
W '=============================================================================='
W ''

Set-Location $RepoRoot
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) {
    W 'FAIL: not a git repository root.'; Save; exit 2
}

# ---- [0a] discovery via docker ----------------------------------------------

W '[0a] DISCOVERY - docker compose labels'
$composeDirs = New-Object System.Collections.Generic.List[string]
$dk = D @('ps', '-a', '--format', '{{.Names}}')
if ($dk.Code -ne 0) {
    W '    docker unavailable - skipping this channel'
} else {
    $names = @($dk.Out | Where-Object { $_ -ne '' })
    W ('    containers seen: ' + (@($names) -join ', '))
    foreach ($n in $names) {
        $ins = D @('inspect', '--format', '{{ index .Config.Labels "com.docker.compose.project.working_dir" }}', $n)
        if ($ins.Code -eq 0) {
            $wd = ($ins.Out -join '').Trim()
            if ($wd -ne '' -and $wd -ne '<no value>' -and -not $composeDirs.Contains($wd)) {
                $composeDirs.Add($wd)
                $exists = Test-Path $wd
                W ('      ' + $n.PadRight(28) + ' -> ' + $wd + $(if ($exists) { '' } else { '   [PATH GONE]' }))
            }
        }
    }
    if ($composeDirs.Count -eq 0) { W '    no compose working_dir labels found' }
}
W ''

# ---- [0b] discovery via filesystem ------------------------------------------

W '[0b] DISCOVERY - filesystem sweep'
$fleetHits = New-Object System.Collections.Generic.List[string]
$frHits    = New-Object System.Collections.Generic.List[string]

$sweepRoots = New-Object System.Collections.Generic.List[string]
foreach ($sr in @('C:\Workspace', $env:USERPROFILE, (Join-Path $env:USERPROFILE 'Downloads'), (Join-Path $env:USERPROFILE 'Desktop'), (Join-Path $env:USERPROFILE 'Documents'))) {
    if ($sr -and (Test-Path $sr)) { $sweepRoots.Add($sr) }
}
foreach ($dr in (Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Name -ne 'C' })) {
    $sweepRoots.Add($dr.Root)
}
foreach ($cd in $composeDirs) {
    if (Test-Path $cd) { $sweepRoots.Add($cd) }
}

foreach ($root in ($sweepRoots | Select-Object -Unique)) {
    $depth = 3
    if ($root -match '^[A-Z]:\\$') { $depth = 2 }
    $dirs = Get-ChildItem -Path $root -Directory -Recurse -Depth $depth -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '(?i)fleet|emulation|ppiq.*(source|src)|source.*system' }
    foreach ($d in $dirs) {
        if (-not $fleetHits.Contains($d.FullName)) { $fleetHits.Add($d.FullName) }
    }
    $frs = Get-ChildItem -Path $root -Filter 'FLEET_RELATIONS*.md' -Recurse -Depth ($depth + 2) -File -ErrorAction SilentlyContinue
    foreach ($f in $frs) {
        if (-not $frHits.Contains($f.FullName)) { $frHits.Add($f.FullName) }
    }
}
W ('    fleet-shaped folders: ' + $fleetHits.Count)
foreach ($h in $fleetHits) { W ('      ' + $h) }
W ('    FLEET_RELATIONS files: ' + $frHits.Count)
foreach ($h in $frHits) { W ('      ' + $h) }
W ''

# ---- [1] FLEET_RELATIONS commit ---------------------------------------------

W '[1] FLEET_RELATIONS'
$r = G @('ls-files', 'docs/emulation/')
$tracked = @($r.Out | Where-Object { $_ -match 'FLEET_RELATIONS' })
if (@($tracked).Count -gt 0) {
    W ('    committed: ' + ($tracked -join ', '))
} elseif ($frHits.Count -gt 0) {
    $src = $frHits[0]
    $dstDir = Join-Path $RepoRoot 'docs\emulation'
    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
    $dst = Join-Path $dstDir 'FLEET_RELATIONS.md'
    Copy-Item -LiteralPath $src -Destination $dst -Force
    $null = G @('add', '--', 'docs/emulation/FLEET_RELATIONS.md')
    $r3 = G @('commit', '-m', 'M1-12: commit FLEET_RELATIONS emulation provenance (R1-R17, seed=42)')
    if ($r3.Code -eq 0) {
        W ('    rescued from ' + $src)
        W ('    committed    ' + ((G @('rev-parse', 'HEAD')).Out[0]).Substring(0, 12))
    } else {
        W ('    COMMIT FAILED: ' + ($r3.Out -join ' ')); $fail++
    }
} else {
    W '    STILL MISSING after full sweep. Two possibilities:'
    W '      a) it lives on another machine / cloud - fetch it and re-run;'
    W '      b) it was only ever in a chat session - recover it from the session'
    W '         outputs (the 16-Jul state assessment cites it) and save it to'
    W '         docs\emulation\FLEET_RELATIONS.md, then re-run.'
    W '    The planted-relations provenance is NOT reconstructable from the DB.'
    $fail++
}
W ''

# ---- [2] emulation root selection -------------------------------------------

W '[2] EMULATION ROOT'
if ([string]::IsNullOrWhiteSpace($EmulationRoot)) {
    foreach ($cand in ($composeDirs + $fleetHits)) {
        if (Test-Path $cand) {
            $sqlCount = @(Get-ChildItem -Path $cand -Filter '*.sql' -Recurse -File -ErrorAction SilentlyContinue).Count
            $ymlCount = @(Get-ChildItem -Path $cand -Include '*.yml', '*.yaml' -Recurse -File -ErrorAction SilentlyContinue).Count
            if (($sqlCount + $ymlCount) -gt 0) {
                $EmulationRoot = $cand
                W ('    selected: ' + $cand + '   (sql=' + $sqlCount + ', compose=' + $ymlCount + ')')
                break
            }
        }
    }
}
if ([string]::IsNullOrWhiteSpace($EmulationRoot) -or -not (Test-Path $EmulationRoot)) {
    W '    NO ASSET FOLDER FOUND. The seeds may exist only inside docker volumes.'
    W '    FALLBACK (run these, they dump the live sources to files you can archive):'
    W '      docker exec <meltshop-pg>  pg_dump -U <user> -d <db> > meltshop_dump.sql'
    W '      docker exec <parsytec-mysql> sh -c "mysqldump -u<user> -p<pw> <db>" > parsytec_dump.sql'
    W '      (Oracle caster/hsm: expdp or a table-level spool)'
    W '    Then re-run with -EmulationRoot pointing at the dump folder.'
    $fail++
    Save; exit 1
}

$patterns = @('*.sql', '*.yml', '*.yaml', '*.csv', '*.md', '*.ps1', '*.sh')
$assets = @()
foreach ($p in $patterns) {
    $assets += Get-ChildItem -Path $EmulationRoot -Filter $p -Recurse -File -ErrorAction SilentlyContinue
}
$assets += Get-ChildItem -Path $EmulationRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'Dockerfile*' }
$assets = @($assets | Sort-Object FullName -Unique)
if (@($assets).Count -eq 0) {
    W '    FAIL: folder found but holds no archivable assets.'; $fail++; Save; exit 1
}
$totalMb = [math]::Round((($assets | Measure-Object Length -Sum).Sum / 1MB), 1)
W ('    files: ' + @($assets).Count + '   total ' + $totalMb + ' MB')
W ''

# ---- [3] archive with manifest ----------------------------------------------

W '[3] ARCHIVE'
$workDir  = Join-Path $RepoRoot ('.ppiq-continuity-' + $stamp)
New-Item -ItemType Directory -Path $workDir -Force | Out-Null
$manifest = Join-Path $workDir 'MANIFEST.sha256.txt'
$mLines   = New-Object System.Collections.Generic.List[string]
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
$zipSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash
W ('    zip       ' + $zipPath + '   (' + ([math]::Round(((Get-Item -LiteralPath $zipPath).Length / 1MB), 1)) + ' MB)')
W ('    sha256    ' + $zipSha)
W ('    manifest  ' + (@($assets).Count) + ' entries')
W ''

# ---- [4] repo bundle ----------------------------------------------------------

W '[4] REPO BUNDLE'
$parent = Split-Path -Parent $RepoRoot
$bundle = Get-ChildItem -Path $parent -Filter 'PPIQ_main_*.bundle' -ErrorAction SilentlyContinue |
          Sort-Object Name -Descending | Select-Object -First 1
if ($bundle) {
    $bundlePath = $bundle.FullName
    W ('    using ' + $bundlePath)
} else {
    $bundlePath = Join-Path $parent ('PPIQ_main_' + $stamp + '.bundle')
    $r = G @('bundle', 'create', $bundlePath, '--all')
    if ($r.Code -ne 0) { W ('    bundle create FAILED: ' + ($r.Out -join ' ')); $fail++; $bundlePath = '' }
    else { W ('    created ' + $bundlePath) }
}
if ($bundlePath -ne '') {
    $r = G @('bundle', 'verify', $bundlePath)
    if ($r.Code -ne 0) { W '    bundle VERIFY FAILED'; $fail++ }
    else { W '    bundle verified OK' }
}
W ''

# ---- [5] off-machine copy -----------------------------------------------------

W '[5] OFF-MACHINE COPY'
if ([string]::IsNullOrWhiteSpace($ArchiveTarget)) {
    W '    NO -ArchiveTarget GIVEN - archive is still on this laptop. Re-run with'
    W '    -ArchiveTarget "E:\PPIQ-Archive" or copy the three artifacts by hand.'
    $fail++
} else {
    if (-not (Test-Path $ArchiveTarget)) { New-Item -ItemType Directory -Path $ArchiveTarget -Force | Out-Null }
    $items = @($zipPath, $manifest)
    if ($bundlePath -ne '') { $items += $bundlePath }
    foreach ($src in $items) {
        $sha1 = (Get-FileHash -Algorithm SHA256 -LiteralPath $src).Hash
        $dest = Join-Path $ArchiveTarget (Split-Path -Leaf $src)
        Copy-Item -LiteralPath $src -Destination $dest -Force
        $sha2 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dest).Hash
        if ($sha2 -eq $sha1) { W ('    copied + hash-verified   ' + $dest) }
        else { W ('    HASH MISMATCH            ' + $dest); $fail++ }
    }
}
W ''

W '=============================================================================='
if ($fail -eq 0) {
    W 'M1-12: COMPLETE. Assets + provenance + history are off this machine.'
} else {
    W ('M1-12: ' + $fail + ' item(s) OPEN - see the sections above.')
}
W '=============================================================================='
Save
if ($fail -eq 0) { exit 0 } else { exit 1 }
