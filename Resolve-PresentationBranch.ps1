<#
.SYNOPSIS
    Resolve-PresentationBranch.ps1 - closes M1-18 for real: second merge of the
    10 stranded 17-Jul commits into main, then branch deletion, then a fresh
    bundle that finally contains ALL the work, copied off-machine.

.DESCRIPTION
    Evidence (M1-18_Complete_v3_133220): 10 commits exist on presentation and
    not on main - among them the M2-18 rebuild script, the D1 provider fix, the
    M1-25 test deletion, live dashboard charts, website v2, and Backlog v23
    docs. They were committed on presentation AFTER the ab9771f9 merge. The
    shared working tree masked it; main's HISTORY is a day behind.

    Cure: merge presentation into main a second time. One merge commit absorbs
    all ten, git then certifies the branch fully merged, and --delete succeeds
    without force. No cherry-picking, no history rewriting, nothing lost.

    Steps:
      [0] preflight - on main; working tree inspected. Uncommitted changes are
          committed as a named pre-merge snapshot ONLY with -AutoCommitWip
          (a merge over a dirty tree is how conflicts eat work).
      [1] patch-equivalence report (git cherry): shows which of the 10 are
          already on main by content (-) vs genuinely missing (+).
      [2] merge --no-ff --no-edit. On conflict: aborted cleanly + reported.
      [3] build gate (dotnet build) unless -SkipBuild.
      [4] branch --delete (no force needed anymore).
      [5] fresh --all bundle + verify + copy to -BundleTarget (defaults to
          OneDrive\PPIQ-Archive when OneDrive exists), hash-verified.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Resolve-PresentationBranch.ps1 -AutoCommitWip
#>

[CmdletBinding()]
param(
    [string]$RepoRoot     = (Get-Location).Path,
    [string]$BranchName   = 'presentation',
    [string]$BundleTarget = '',
    [switch]$AutoCommitWip,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("M1-18_Resolve_" + $stamp + ".txt")
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
W ('M1-18 RESOLVE (second merge) - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''

Set-Location $RepoRoot
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) { W 'FAIL: not a repo root.'; Save; exit 2 }

# ---- [0] preflight ------------------------------------------------------------

W '[0] PREFLIGHT'
$branch = (G @('rev-parse', '--abbrev-ref', 'HEAD')).Out[0]
if ($branch -ne 'main') { W ('    FAIL: on ' + $branch + ', not main.'); Save; exit 2 }
$r = G @('rev-parse', '--verify', '--quiet', ('refs/heads/' + $BranchName))
if ($r.Code -ne 0) { W ('    branch ' + $BranchName + ' gone - nothing to resolve.'); Save; exit 0 }

$st = G @('status', '--porcelain=v1')
$dirty = @($st.Out | Where-Object { $_ -ne '' })
if (@($dirty).Count -gt 0) {
    W ('    working tree has ' + @($dirty).Count + ' uncommitted change(s):')
    foreach ($d in ($dirty | Select-Object -First 15)) { W ('      ' + $d) }
    if (@($dirty).Count -gt 15) { W ('      ... and ' + (@($dirty).Count - 15) + ' more') }
    if (-not $AutoCommitWip) {
        W ''
        W '    REFUSING to merge over a dirty tree. Either commit these yourself in'
        W '    logical units (preferred), or re-run with -AutoCommitWip to snapshot'
        W '    them as one pre-merge commit.'
        Save; exit 1
    }
    $null = G @('add', '-A')
    $r = G @('commit', '-m', ('M1-18: pre-merge working-tree snapshot (' + $stamp + ')'))
    if ($r.Code -eq 0) { W ('    snapshot committed          ' + ((G @('rev-parse','HEAD')).Out[0]).Substring(0,12)) }
    else { W ('    snapshot commit FAILED: ' + ($r.Out -join ' ')); Save; exit 1 }
} else {
    W '    working tree clean'
}
W ''

# ---- [1] patch-equivalence report ---------------------------------------------

W '[1] PATCH EQUIVALENCE (git cherry: "-" = already on main by content, "+" = missing)'
$r = G @('cherry', '-v', 'main', $BranchName)
foreach ($l in $r.Out) { W ('    ' + $l) }
W ''

# ---- [2] the second merge -----------------------------------------------------

W '[2] MERGE'
$preSha = (G @('rev-parse', 'HEAD')).Out[0]
$r = G @('merge', '--no-ff', '--no-edit', $BranchName)
if ($r.Code -ne 0) {
    W '    MERGE CONFLICT. Conflicting files:'
    $cf = G @('diff', '--name-only', '--diff-filter=U')
    foreach ($f in $cf.Out) { W ('      ' + $f) }
    $null = G @('merge', '--abort')
    W ('    merge aborted cleanly; main restored to ' + $preSha.Substring(0, 12))
    W '    Resolve by hand or send me this log - do not force anything.'
    $fail++
    Save; exit 1
}
$mergeSha = (G @('rev-parse', 'HEAD')).Out[0]
W ('    merged ' + $BranchName + ' -> main   ' + $mergeSha.Substring(0, 12))
$r = G @('diff', '--stat', $preSha, 'HEAD')
$statTail = @($r.Out | Select-Object -Last 1)
W ('    ' + ($statTail -join ''))
W ''

# ---- [3] build gate -----------------------------------------------------------

if ($SkipBuild) {
    W '[3] BUILD GATE skipped (-SkipBuild). UNPROVEN until it builds.'
} else {
    W '[3] BUILD GATE'
    $sln = Join-Path $RepoRoot 'Backend\PlantProcessIQ.sln'
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $out = & dotnet build $sln -c Debug --nologo -v q 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($code -ne 0) {
        W '    BUILD FAILED after merge:'
        foreach ($l in $out) { if ("$l" -match '(?i)error') { W ('      ' + $l) } }
        W '    Main is at the merge commit; nothing reverted automatically because'
        W '    the merge itself is likely correct and the error needs eyes. Send the log.'
        $fail++
        Save; exit 1
    }
    W '    build green'
}
W ''

# ---- [4] branch deletion ------------------------------------------------------

W '[4] BRANCH DELETION'
$r = G @('branch', '--delete', $BranchName)
if ($r.Code -eq 0) { W ('    deleted ' + $BranchName + ' (fully merged - no force needed)') }
else { W ('    refused: ' + ($r.Out -join ' ')); $fail++ }
W ''

# ---- [5] fresh bundle off-machine ---------------------------------------------

W '[5] BUNDLE (fresh - the old one predates this merge)'
$parent = Split-Path -Parent $RepoRoot
$bundlePath = Join-Path $parent ('PPIQ_main_' + $stamp + '.bundle')
$r = G @('bundle', 'create', $bundlePath, '--all')
if ($r.Code -ne 0) { W '    bundle create FAILED'; $fail++ }
else {
    $r = G @('bundle', 'verify', $bundlePath)
    if ($r.Code -ne 0) { W '    verify FAILED'; $fail++ }
    else {
        $sha1 = (Get-FileHash -Algorithm SHA256 -LiteralPath $bundlePath).Hash
        W ('    created + verified   ' + $bundlePath)
        if ([string]::IsNullOrWhiteSpace($BundleTarget)) {
            if ($env:OneDrive -and (Test-Path $env:OneDrive)) {
                $BundleTarget = Join-Path $env:OneDrive 'PPIQ-Archive'
                W ('    defaulting target to ' + $BundleTarget)
            }
        }
        if ([string]::IsNullOrWhiteSpace($BundleTarget)) {
            W '    no target available - copy the bundle off-machine by hand.'
            $fail++
        } else {
            if (-not (Test-Path $BundleTarget)) { New-Item -ItemType Directory -Path $BundleTarget -Force | Out-Null }
            $dest = Join-Path $BundleTarget (Split-Path -Leaf $bundlePath)
            Copy-Item -LiteralPath $bundlePath -Destination $dest -Force
            $sha2 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dest).Hash
            if ($sha2 -eq $sha1) { W ('    copied + hash-verified   ' + $dest) }
            else { W '    HASH MISMATCH on copy'; $fail++ }
        }
    }
}
W ''

W '=============================================================================='
if ($fail -eq 0) {
    W 'M1-18: COMPLETE. One trunk, full history, branch gone, bundle off-machine.'
} else {
    W ('M1-18: ' + $fail + ' item(s) open.')
}
W '=============================================================================='
Save
if ($fail -eq 0) { exit 0 } else { exit 1 }
