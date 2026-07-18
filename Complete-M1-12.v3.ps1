<#
.SYNOPSIS
    Complete-M1-12.v3.ps1 - continuity via the channel that cannot lie: the six
    ppiq-src-* containers' bind mounts name the host folders their seed SQLs
    came from. Crash-proof filesystem sweep as the second channel.

.DESCRIPTION
    v2 findings this script answers:
      - containers carry NO compose labels (started via docker run) -> [0a] now
        reads .Mounts and HostConfig.Binds from every ppiq-src-* container; any
        bind source is a host path that held the init SQLs.
      - a dead mapped network drive threw a terminating IOException that
        -ErrorAction cannot suppress -> every sweep root is probed inside
        try/catch and unreachable roots are skipped with a note, never a crash.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-12.v3.ps1 -ArchiveTarget "D:\PPIQ-Archive"
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
$logPath = Join-Path $RepoRoot ("M1-12_Complete_v3_" + $stamp + ".txt")
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
function ToWinPath([string]$p) {
    if ($p -match '^/host_mnt/([a-zA-Z])/(.*)$') { return ($Matches[1].ToUpper() + ':\' + ($Matches[2] -replace '/', '\')) }
    if ($p -match '^/([a-zA-Z])/(.*)$') { return ($Matches[1].ToUpper() + ':\' + ($Matches[2] -replace '/', '\')) }
    if ($p -match '^[a-zA-Z]:[\\/]') { return ($p -replace '/', '\') }
    return $p
}

W '=============================================================================='
W ('M1-12 CONTINUITY v3 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''

Set-Location $RepoRoot
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) { W 'FAIL: not a repo root.'; Save; exit 2 }

# ---- [0a] discovery: container bind mounts -----------------------------------

W '[0a] DISCOVERY - ppiq-src-* container mounts'
$mountDirs = New-Object System.Collections.Generic.List[string]
$dk = D @('ps', '-a', '--format', '{{.Names}}')
if ($dk.Code -ne 0) {
    W '    docker unavailable'
} else {
    $srcNames = @($dk.Out | Where-Object { $_ -match '^ppiq-src-' })
    foreach ($n in $srcNames) {
        $ins = D @('inspect', '--format', '{{range .Mounts}}{{.Type}}|{{.Source}}|{{.Destination}};{{end}}', $n)
        if ($ins.Code -ne 0) { continue }
        $entries = (($ins.Out -join '') -split ';') | Where-Object { $_ -ne '' }
        foreach ($e in $entries) {
            $parts = $e -split '\|'
            if ($parts.Count -lt 3) { continue }
            $type = $parts[0]; $src = $parts[1]; $dst = $parts[2]
            W ('    ' + $n.PadRight(28) + $type.PadRight(8) + $src + '  ->  ' + $dst)
            if ($type -eq 'bind') {
                $winSrc = ToWinPath $src
                $dir = $winSrc
                if (Test-Path -LiteralPath $winSrc -PathType Leaf) { $dir = Split-Path -Parent $winSrc }
                if ((Test-Path -LiteralPath $dir) -and -not $mountDirs.Contains($dir)) { $mountDirs.Add($dir) }
            }
        }
    }
    if (@($srcNames).Count -eq 0) { W '    no ppiq-src-* containers found' }
    if ($mountDirs.Count -gt 0) {
        W ''
        W '    HOST FOLDERS BEHIND THE SOURCES:'
        foreach ($m in $mountDirs) { W ('      ' + $m) }
    } else {
        W ''
        W '    no reachable bind sources - the seeds live only inside images/volumes.'
        W '    They are still recoverable: docker cp <container>:/docker-entrypoint-initdb.d .'
    }
}
W ''

# ---- [0b] discovery: crash-proof filesystem sweep ----------------------------

W '[0b] DISCOVERY - filesystem sweep (unreachable roots skipped, never fatal)'
$fleetHits = New-Object System.Collections.Generic.List[string]
$frHits    = New-Object System.Collections.Generic.List[string]

$sweepRoots = New-Object System.Collections.Generic.List[string]
foreach ($sr in @('C:\Workspace', $env:USERPROFILE, (Join-Path $env:USERPROFILE 'Downloads'), (Join-Path $env:USERPROFILE 'Desktop'), (Join-Path $env:USERPROFILE 'Documents'), $env:OneDrive)) {
    if ($sr) { $sweepRoots.Add($sr) }
}
foreach ($dr in (Get-PSDrive -PSProvider FileSystem)) {
    if ($dr.Name -ne 'C') { $sweepRoots.Add($dr.Root) }
}
foreach ($m in $mountDirs) { $sweepRoots.Add($m) }

foreach ($root in ($sweepRoots | Select-Object -Unique)) {
    $ok = $false
    try { $ok = Test-Path -LiteralPath $root -ErrorAction Stop } catch { $ok = $false }
    if (-not $ok) { W ('    skip (unreachable): ' + $root); continue }
    $depth = 3
    if ($root -match '^[A-Za-z]:\\$') { $depth = 2 }
    try {
        $dirs = @(Get-ChildItem -LiteralPath $root -Directory -Recurse -Depth $depth -Force -ErrorAction SilentlyContinue |
                  Where-Object { $_.Name -match '(?i)fleet|emulat|ppiq.*(src|source)|source-?system' })
        foreach ($d in $dirs) {
            if (-not $fleetHits.Contains($d.FullName)) { $fleetHits.Add($d.FullName) }
        }
        $frs = @(Get-ChildItem -LiteralPath $root -Filter 'FLEET_RELATIONS*.md' -Recurse -Depth ($depth + 2) -File -Force -ErrorAction SilentlyContinue)
        foreach ($f in $frs) {
            if (-not $frHits.Contains($f.FullName)) { $frHits.Add($f.FullName) }
        }
    } catch {
        W ('    skip (error mid-sweep): ' + $root + ' :: ' + $_.Exception.Message)
    }
}
W ('    fleet-shaped folders: ' + $fleetHits.Count)
foreach ($h in $fleetHits) { W ('      ' + $h) }
W ('    FLEET_RELATIONS files: ' + $frHits.Count)
foreach ($h in $frHits) { W ('      ' + $h) }
W ''

# ---- [1] FLEET_RELATIONS ------------------------------------------------------

W '[1] FLEET_RELATIONS'
$r = G @('ls-files', 'docs/emulation/')
$tracked = @($r.Out | Where-Object { $_ -match 'FLEET_RELATIONS' })
if (@($tracked).Count -gt 0) {
    W ('    committed: ' + ($tracked -join ', '))
} elseif ($frHits.Count -gt 0) {
    $src = $frHits[0]
    $dstDir = Join-Path $RepoRoot 'docs\emulation'
    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
    Copy-Item -LiteralPath $src -Destination (Join-Path $dstDir 'FLEET_RELATIONS.md') -Force
    $null = G @('add', '--', 'docs/emulation/FLEET_RELATIONS.md')
    $r3 = G @('commit', '-m', 'M1-12: commit FLEET_RELATIONS emulation provenance (R1-R17, seed=42)')
    if ($r3.Code -eq 0) {
        W ('    rescued from ' + $src)
        W ('    committed    ' + ((G @('rev-parse', 'HEAD')).Out[0]).Substring(0, 12))
    } else { W ('    COMMIT FAILED: ' + ($r3.Out -join ' ')); $fail++ }
} else {
    W '    NOT FOUND on this machine. It must be reconstructed from the session'
    W '    record (the 16-Jul assessment cites its contents) - ask for the'
    W '    reconstruction, save to docs\emulation\FLEET_RELATIONS.md, re-run.'
    $fail++
}
W ''

# ---- [2] emulation root -------------------------------------------------------

W '[2] EMULATION ROOT'
if ([string]::IsNullOrWhiteSpace($EmulationRoot)) {
    foreach ($cand in (@($mountDirs) + @($fleetHits))) {
        if (-not (Test-Path -LiteralPath $cand)) { continue }
        $sqlCount = 0; $ymlCount = 0
        try {
            $sqlCount = @(Get-ChildItem -LiteralPath $cand -Filter '*.sql' -Recurse -File -ErrorAction SilentlyContinue).Count
            $ymlCount = @(Get-ChildItem -LiteralPath $cand -Include '*.yml', '*.yaml' -Recurse -File -ErrorAction SilentlyContinue).Count
        } catch { continue }
        if (($sqlCount + $ymlCount) -gt 0) {
            $EmulationRoot = $cand
            W ('    selected: ' + $cand + '   (sql=' + $sqlCount + ', compose=' + $ymlCount + ')')
            break
        }
    }
}
if ([string]::IsNullOrWhiteSpace($EmulationRoot) -or -not (Test-Path -LiteralPath $EmulationRoot)) {
    W '    no host folder with seeds found. RECOVERY FROM THE CONTAINERS THEMSELVES:'
    W '      mkdir C:\Workspace\ppiq-emulation-recovered'
    W '      docker cp ppiq-src-meltshop-postgres:/docker-entrypoint-initdb.d C:\Workspace\ppiq-emulation-recovered\meltshop'
    W '      docker cp ppiq-src-parsytec-mysql:/docker-entrypoint-initdb.d C:\Workspace\ppiq-emulation-recovered\parsytec'
    W '      docker cp ppiq-src-downtime-mysql:/docker-entrypoint-initdb.d C:\Workspace\ppiq-emulation-recovered\downtime'
    W '      (oracle/mssql init dirs: /opt/oracle/scripts/startup and /docker-entrypoint-initdb.d)'
    W '    then re-run with -EmulationRoot C:\Workspace\ppiq-emulation-recovered'
    $fail++
    Save; exit 1
}

$patterns = @('*.sql', '*.yml', '*.yaml', '*.csv', '*.md', '*.ps1', '*.sh')
$assets = @()
foreach ($p in $patterns) {
    $assets += Get-ChildItem -LiteralPath $EmulationRoot -Filter $p -Recurse -File -ErrorAction SilentlyContinue
}
$assets = @($assets | Sort-Object FullName -Unique)
if (@($assets).Count -eq 0) { W '    folder holds no archivable assets.'; $fail++; Save; exit 1 }
W ('    files: ' + @($assets).Count + '   total ' + [math]::Round((($assets | Measure-Object Length -Sum).Sum / 1MB), 1) + ' MB')
W ''

# ---- [3] archive --------------------------------------------------------------

W '[3] ARCHIVE'
$workDir  = Join-Path $RepoRoot ('.ppiq-continuity-' + $stamp)
New-Item -ItemType Directory -Path $workDir -Force | Out-Null
$manifest = Join-Path $workDir 'MANIFEST.sha256.txt'
$mLines   = New-Object System.Collections.Generic.List[string]
$mLines.Add('# PPIQ emulation continuity - ' + $stamp + ' - root: ' + $EmulationRoot)
foreach ($a in $assets) {
    $h = (Get-FileHash -Algorithm SHA256 -LiteralPath $a.FullName).Hash
    $mLines.Add($h + '  ' + $a.FullName.Substring($EmulationRoot.Length).TrimStart('\', '/'))
}
[System.IO.File]::WriteAllText($manifest, (($mLines -join "`r`n") + "`r`n"), $utf8)
$zipPath = Join-Path $workDir ('PPIQ_emulation_' + $stamp + '.zip')
Compress-Archive -Path (Join-Path $EmulationRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
W ('    zip       ' + $zipPath + '   (' + [math]::Round(((Get-Item -LiteralPath $zipPath).Length / 1MB), 1) + ' MB)')
W ('    manifest  ' + (@($assets).Count) + ' entries')
W ''

# ---- [4] off-machine copy ------------------------------------------------------

W '[4] OFF-MACHINE COPY'
$targetOk = $false
if (-not [string]::IsNullOrWhiteSpace($ArchiveTarget)) {
    $qualifier = Split-Path -Qualifier $ArchiveTarget -ErrorAction SilentlyContinue
    if ($qualifier -and (Test-Path ($qualifier + '\'))) { $targetOk = $true }
    if ($ArchiveTarget.StartsWith('\\')) { $targetOk = (Test-Path (Split-Path -Parent $ArchiveTarget)) }
}
if (-not $targetOk) {
    if ([string]::IsNullOrWhiteSpace($ArchiveTarget)) { W '    no -ArchiveTarget given.' }
    else { W ('    TARGET INVALID: ' + $ArchiveTarget) }
    W '    valid candidates:'
    foreach ($d in (Get-PSDrive -PSProvider FileSystem)) {
        if ($d.Name -eq 'C') { continue }
        $reachable = $false
        try { $reachable = Test-Path ($d.Root) } catch { $reachable = $false }
        if ($reachable) {
            $free = ''
            if ($null -ne $d.Free) { $free = '   free ' + [math]::Round($d.Free / 1GB, 1) + ' GB' }
            W ('      ' + $d.Root + $free)
        }
    }
    if ($env:OneDrive -and (Test-Path $env:OneDrive)) { W ('      ' + $env:OneDrive + '   (synced cloud)') }
    $fail++
} else {
    if (-not (Test-Path $ArchiveTarget)) { New-Item -ItemType Directory -Path $ArchiveTarget -Force | Out-Null }
    $parent = Split-Path -Parent $RepoRoot
    $bundle = Get-ChildItem -Path $parent -Filter 'PPIQ_main_*.bundle' -ErrorAction SilentlyContinue |
              Sort-Object Name -Descending | Select-Object -First 1
    $items = @($zipPath, $manifest)
    if ($bundle) { $items += $bundle.FullName }
    foreach ($srcItem in $items) {
        $sha1 = (Get-FileHash -Algorithm SHA256 -LiteralPath $srcItem).Hash
        $dest = Join-Path $ArchiveTarget (Split-Path -Leaf $srcItem)
        Copy-Item -LiteralPath $srcItem -Destination $dest -Force
        $sha2 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dest).Hash
        if ($sha2 -eq $sha1) { W ('    copied + hash-verified   ' + $dest) }
        else { W ('    HASH MISMATCH            ' + $dest); $fail++ }
    }
}
W ''

W '=============================================================================='
if ($fail -eq 0) { W 'M1-12: COMPLETE.' }
else { W ('M1-12: ' + $fail + ' item(s) open - see above.') }
W '=============================================================================='
Save
if ($fail -eq 0) { exit 0 } else { exit 1 }
