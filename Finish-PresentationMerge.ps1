<#
.SYNOPSIS
    Finish-PresentationMerge.ps1 - completes the M1-18 second merge: re-runs the
    merge, resolves the single .gitignore conflict by LINE UNION of both sides,
    commits, gates, deletes the branch, ships a fresh bundle to OneDrive.

.DESCRIPTION
    Evidence (M1-18_Resolve_140317): the merge conflicts on exactly one file,
    .gitignore - main's version carries the evidence-artifact rules appended
    18-Jul, presentation's commit bebc8b23 carries the session-artifact rules
    committed 17-Jul. Both are wanted. The correct resolution is the union.

    The union is computed from the index stages (:2 = ours/main, :3 = theirs/
    presentation), not from any working-tree file: every line of ours in order,
    then every line of theirs not already present. Nothing is dropped.

    Safety: if the merge conflicts on ANY file other than .gitignore, the merge
    is aborted cleanly and the list is reported - no automatic resolution is
    attempted on real code.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Finish-PresentationMerge.ps1
#>

[CmdletBinding()]
param(
    [string]$RepoRoot     = (Get-Location).Path,
    [string]$BranchName   = 'presentation',
    [string]$BundleTarget = '',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("M1-18_FinishMerge_" + $stamp + ".txt")
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
W ('M1-18 FINISH MERGE - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''

Set-Location $RepoRoot
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) { W 'FAIL: not a repo root.'; Save; exit 2 }

# ---- [0] preflight ------------------------------------------------------------

W '[0] PREFLIGHT'
$branch = (G @('rev-parse', '--abbrev-ref', 'HEAD')).Out[0]
if ($branch -ne 'main') { W ('    FAIL: on ' + $branch + ', not main.'); Save; exit 2 }
$r = G @('rev-parse', '--verify', '--quiet', ('refs/heads/' + $BranchName))
if ($r.Code -ne 0) { W ('    ' + $BranchName + ' gone - nothing to do.'); Save; exit 0 }
$mergeHead = Join-Path $RepoRoot '.git\MERGE_HEAD'
if (Test-Path $mergeHead) {
    W '    a merge is already in progress - continuing with THAT merge state.'
} else {
    $st = @((G @('status', '--porcelain=v1')).Out | Where-Object { $_ -ne '' })
    if (@($st).Count -gt 0) {
        W ('    working tree not clean (' + @($st).Count + ' entries):')
        foreach ($d in ($st | Select-Object -First 10)) { W ('      ' + $d) }
        W '    Commit or remove these first (the resolver refuses to guess).'
        Save; exit 1
    }
    W '    on main, clean tree'
}
W ''

# ---- [1] merge ----------------------------------------------------------------

W '[1] MERGE'
$preSha = (G @('rev-parse', 'HEAD')).Out[0]
if (-not (Test-Path $mergeHead)) {
    $r = G @('merge', '--no-ff', '--no-edit', $BranchName)
    if ($r.Code -eq 0) {
        W '    merged clean - no conflict this time.'
        $merged = $true
    } else {
        $merged = $false
    }
} else {
    $merged = $false
}

if (-not $merged) {
    $cf = @((G @('diff', '--name-only', '--diff-filter=U')).Out | Where-Object { $_ -ne '' })
    W ('    conflicts: ' + (@($cf) -join ', '))
    $unexpected = @($cf | Where-Object { $_ -ne '.gitignore' })
    if (@($unexpected).Count -gt 0) {
        W '    UNEXPECTED conflict files beyond .gitignore - aborting, no auto-resolution:'
        foreach ($u in $unexpected) { W ('      ' + $u) }
        $null = G @('merge', '--abort')
        W ('    merge aborted; main restored to ' + $preSha.Substring(0, 12))
        $fail++
        Save; exit 1
    }
    if (@($cf).Count -eq 0) {
        W '    no unresolved files (already resolved?) - proceeding to commit.'
    } else {
        # ---- union resolution from the index stages -----------------------------
        W ''
        W '    resolving .gitignore by LINE UNION (ours + theirs-not-in-ours):'
        $ours   = (G @('show', ':2:.gitignore')).Out
        $theirs = (G @('show', ':3:.gitignore')).Out
        $seen   = New-Object 'System.Collections.Generic.HashSet[string]'
        $result = New-Object System.Collections.Generic.List[string]
        foreach ($l in $ours) {
            $result.Add($l)
            $t = $l.Trim()
            if ($t -ne '' -and -not $t.StartsWith('#')) { $null = $seen.Add($t) }
        }
        $added = 0
        foreach ($l in $theirs) {
            $t = $l.Trim()
            if ($t -eq '' -or $t.StartsWith('#')) { continue }
            if (-not $seen.Contains($t)) {
                if ($added -eq 0) { $result.Add(''); $result.Add('# M1-18 merge union: rules present only on presentation') }
                $result.Add($l)
                $null = $seen.Add($t)
                $added++
                W ('      + ' + $l)
            }
        }
        if ($added -eq 0) { W '      (theirs is a subset of ours - union = ours, nothing added)' }
        [System.IO.File]::WriteAllText((Join-Path $RepoRoot '.gitignore'), (($result -join "`r`n") + "`r`n"), $utf8)
        $null = G @('add', '--', '.gitignore')
        W ('    resolved: ' + $result.Count + ' lines total, ' + $added + ' line(s) adopted from ' + $BranchName)
    }
    $r = G @('commit', '--no-edit')
    if ($r.Code -ne 0) { W ('    merge commit FAILED: ' + ($r.Out -join ' ')); $fail++; Save; exit 1 }
}
$mergeSha = (G @('rev-parse', 'HEAD')).Out[0]
W ('    merge commit                ' + $mergeSha.Substring(0, 12))
$r = G @('log', '--oneline', ('main..' + $BranchName))
$left = @($r.Out | Where-Object { $_ -ne '' })
W ('    commits still unmerged      ' + @($left).Count + '   (must be 0)')
if (@($left).Count -ne 0) { $fail++ }
W ''

# ---- [2] build gate -----------------------------------------------------------

if ($SkipBuild) {
    W '[2] BUILD GATE skipped (-SkipBuild). UNPROVEN until it builds.'
} else {
    W '[2] BUILD GATE'
    $sln = Join-Path $RepoRoot 'Backend\PlantProcessIQ.sln'
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $out = & dotnet build $sln -c Debug --nologo -v q 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($code -ne 0) {
        W '    BUILD FAILED after merge:'
        foreach ($l in $out) { if ("$l" -match '(?i)error') { W ('      ' + $l) } }
        W '    Main sits at the merge commit for inspection - send the log.'
        $fail++
        Save; exit 1
    }
    W '    build green'
}
W ''

# ---- [3] branch deletion ------------------------------------------------------

W '[3] BRANCH DELETION'
$r = G @('branch', '--delete', $BranchName)
if ($r.Code -eq 0) { W ('    deleted ' + $BranchName + ' (fully merged, no force)') }
else { W ('    refused: ' + ($r.Out -join ' ')); $fail++ }
W ''

# ---- [4] fresh bundle off-machine ---------------------------------------------

W '[4] BUNDLE'
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
            if ($env:OneDrive -and (Test-Path $env:OneDrive)) { $BundleTarget = Join-Path $env:OneDrive 'PPIQ-Archive' }
        }
        if ([string]::IsNullOrWhiteSpace($BundleTarget)) {
            W '    no target - copy off-machine by hand.'; $fail++
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
    W 'M1-18: COMPLETE. One trunk with all 17-Jul work, branch gone, fresh bundle'
    W 'off-machine. The 13:24 bundle in OneDrive is now superseded - keep or delete.'
} else {
    W ('M1-18: ' + $fail + ' item(s) open.')
}
W '=============================================================================='
Save
if ($fail -eq 0) { exit 0 } else { exit 1 }
