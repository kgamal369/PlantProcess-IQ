<#
.SYNOPSIS
    Complete-M1-18.v3.ps1 - the last two M1-18 items: (A) evidence report on the
    unmerged presentation commits, with safe archive-then-delete; (B) off-machine
    copy of the existing verified bundle, with target validation.

.DESCRIPTION
    v2 findings this script answers:
      - git REFUSED --delete: presentation holds commits that are NOT on main.
        [1] prints every such commit with files touched. NOTHING is deleted by
        default. With -ArchiveAndDelete the branch tip is first preserved as an
        immutable tag (archive/presentation-<stamp>) so zero history is lost,
        and only then force-deleted. The 256MB --all bundle already contains it.
      - E:\ does not exist. [2] validates the target drive BEFORE touching it,
        and when invalid, lists every real candidate (fixed/removable drives
        with free space + OneDrive) so the re-run is one copy-paste.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-18.v3.ps1
    (report only - shows the unmerged commits and the valid copy targets)

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-18.v3.ps1 -BundleTarget "D:\PPIQ-Archive" -ArchiveAndDelete
#>

[CmdletBinding()]
param(
    [string]$RepoRoot     = (Get-Location).Path,
    [string]$BranchName   = 'presentation',
    [string]$BundleTarget = '',
    [switch]$ArchiveAndDelete
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("M1-18_Complete_v3_" + $stamp + ".txt")
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

W '=============================================================================='
W ('M1-18 COMPLETION v3 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''

Set-Location $RepoRoot
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) { W 'FAIL: not a repo root.'; Save; exit 2 }

# ---- [1] the unmerged commits ------------------------------------------------

W ('[1] UNMERGED COMMITS ON ' + $BranchName)
$r = G @('rev-parse', '--verify', '--quiet', ('refs/heads/' + $BranchName))
if ($r.Code -ne 0) {
    W '    branch does not exist locally - already resolved'
} else {
    $r = G @('log', '--oneline', ('main..' + $BranchName))
    $ahead = @($r.Out | Where-Object { $_ -ne '' })
    if (@($ahead).Count -eq 0) {
        W '    zero unmerged commits (git may now allow --delete; trying)'
        $r2 = G @('branch', '--delete', $BranchName)
        if ($r2.Code -eq 0) { W ('    deleted ' + $BranchName) }
        else { W ('    still refused: ' + ($r2.Out -join ' ')); $fail++ }
    } else {
        W ('    ' + @($ahead).Count + ' commit(s) exist on ' + $BranchName + ' but NOT on main:')
        W ''
        foreach ($c in $ahead) { W ('      ' + $c) }
        W ''
        W '    files they touch (vs main):'
        $r3 = G @('diff', '--stat', 'main...' + $BranchName)
        foreach ($l in ($r3.Out | Select-Object -Last 30)) { W ('      ' + $l) }
        W ''
        if (-not $ArchiveAndDelete) {
            W '    DECISION REQUIRED. Read the commits above, then either:'
            W '      a) they contain WANTED work missing from main ->'
            W '           git cherry-pick <sha>   (per commit, on main), then re-run this; or'
            W '      b) they are obsolete/duplicated on main ->'
            W '           re-run this script with -ArchiveAndDelete'
            W '    Nothing was deleted.'
        } else {
            $tagName = 'archive/' + $BranchName + '-' + $stamp
            $r4 = G @('tag', $tagName, $BranchName)
            if ($r4.Code -ne 0) {
                W ('    TAG FAILED (' + ($r4.Out -join ' ') + ') - refusing to delete.'); $fail++
            } else {
                W ('    tip preserved as immutable tag: ' + $tagName)
                $r5 = G @('branch', '-D', $BranchName)
                if ($r5.Code -eq 0) {
                    W ('    force-deleted ' + $BranchName + ' (history intact under the tag + bundle)')
                } else {
                    W ('    delete failed: ' + ($r5.Out -join ' ')); $fail++
                }
            }
        }
    }
}
W ''

# ---- [2] off-machine bundle copy ---------------------------------------------

W '[2] BUNDLE OFF-MACHINE'
$parent = Split-Path -Parent $RepoRoot
$bundle = Get-ChildItem -Path $parent -Filter 'PPIQ_main_*.bundle' -ErrorAction SilentlyContinue |
          Sort-Object Name -Descending | Select-Object -First 1
if (-not $bundle) {
    $bundlePath = Join-Path $parent ('PPIQ_main_' + $stamp + '.bundle')
    $r = G @('bundle', 'create', $bundlePath, '--all')
    if ($r.Code -ne 0) { W '    bundle create FAILED'; $fail++; Save; exit 1 }
    $bundle = Get-Item -LiteralPath $bundlePath
    W ('    created fresh ' + $bundle.FullName)
} else {
    W ('    using ' + $bundle.FullName)
}
$r = G @('bundle', 'verify', $bundle.FullName)
if ($r.Code -ne 0) { W '    VERIFY FAILED'; $fail++; Save; exit 1 }
W '    verified OK'

$targetOk = $false
if (-not [string]::IsNullOrWhiteSpace($BundleTarget)) {
    $qualifier = Split-Path -Qualifier $BundleTarget -ErrorAction SilentlyContinue
    if ($qualifier -and (Test-Path ($qualifier + '\'))) { $targetOk = $true }
    if ($BundleTarget.StartsWith('\\')) { $targetOk = (Test-Path (Split-Path -Parent $BundleTarget)) }
}
if (-not $targetOk) {
    W ''
    if ([string]::IsNullOrWhiteSpace($BundleTarget)) { W '    no -BundleTarget given.' }
    else { W ('    TARGET INVALID: ' + $BundleTarget + ' (drive not present).') }
    W '    valid candidates on this machine right now:'
    foreach ($d in (Get-PSDrive -PSProvider FileSystem)) {
        $free = ''
        if ($null -ne $d.Free) { $free = '   free ' + [math]::Round($d.Free / 1GB, 1) + ' GB' }
        $reachable = Test-Path ($d.Root)
        if ($reachable -and $d.Name -ne 'C') { W ('      ' + $d.Root + $free) }
    }
    if ($env:OneDrive -and (Test-Path $env:OneDrive)) {
        W ('      ' + $env:OneDrive + '   (synced cloud - counts as off-machine once synced)')
    }
    W '    re-run with -BundleTarget "<one of the above>\PPIQ-Archive"'
    W '    (the bundle is 256MB; C:-to-C: copies do NOT count as continuity)'
    $fail++
} else {
    if (-not (Test-Path $BundleTarget)) { New-Item -ItemType Directory -Path $BundleTarget -Force | Out-Null }
    $sha1 = (Get-FileHash -Algorithm SHA256 -LiteralPath $bundle.FullName).Hash
    $dest = Join-Path $BundleTarget $bundle.Name
    Copy-Item -LiteralPath $bundle.FullName -Destination $dest -Force
    $sha2 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dest).Hash
    if ($sha2 -eq $sha1) { W ('    copied + hash-verified   ' + $dest) }
    else { W ('    HASH MISMATCH            ' + $dest); $fail++ }
}
W ''

W '=============================================================================='
if ($fail -eq 0) { W 'M1-18: COMPLETE.' }
else { W ('M1-18: ' + $fail + ' item(s) open - see above.') }
W '=============================================================================='
Save
if ($fail -eq 0) { exit 0 } else { exit 1 }
