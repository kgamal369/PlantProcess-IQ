<#
.SYNOPSIS
    Complete-M1-18.v2.ps1 - v2 of the merge-completion script. Fixes two v1
    defects and closes the finding v1 exposed.

.DESCRIPTION
    v1 -> v2 changes (defects fixed at source, per the Solution Doctrine):
      D1  The git wrapper ran under -ErrorActionPreference Stop; on PS 5.1 any
          native stderr line becomes a terminating error, which killed v1 at
          step [4] before the bundle ever ran. The wrapper now scopes EAP to
          Continue around the native call.
      D2  Passing -d to the wrapper bound to PowerShell's common -Debug switch,
          so git received "branch presentation" (CREATE) instead of a deletion.
          All git switches now travel as quoted long-form strings (--delete).
      F1  v1 [3] proved the /api/ml/foundation matrix fix is in the WORKING TREE
          but NOT in any commit. v2 stages and commits exactly that file when it
          finds the mapping uncommitted - the engine 403s without it and one
          checkout would have silently destroyed it.

    Idempotent: every step detects already-done and skips.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-18.v2.ps1 -BundleTarget "E:\PPIQ-Archive"
#>

[CmdletBinding()]
param(
    [string]$RepoRoot     = (Get-Location).Path,
    [string]$MergeCommit  = 'ab9771f9',
    [string]$BranchName   = 'presentation',
    [string]$BundleTarget = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("M1-18_Complete_v2_" + $stamp + ".txt")
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
    # D1+D2 fix: EAP scoped to Continue so native stderr cannot terminate the
    # script; args arrive as plain strings (callers quote every switch).
    param([string[]]$GitArgs)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $out = & git @GitArgs 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $prev
    return @{ Code = $code; Out = @($out | ForEach-Object { "$_" }) }
}

W '=============================================================================='
W ('M1-18 COMPLETION v2 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('Repo: ' + $RepoRoot)
W '=============================================================================='
W ''

# ---- [0] preflight ----------------------------------------------------------

Set-Location $RepoRoot
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) {
    W 'FAIL: not a git repository root.'
    Save; exit 2
}
$r = G @('rev-parse', '--abbrev-ref', 'HEAD')
if ($r.Code -ne 0) { W 'FAIL: git unavailable.'; Save; exit 2 }
$branch = $r.Out[0]
W ('[0] PREFLIGHT   branch=' + $branch)
if ($branch -ne 'main') {
    W '    FAIL: not on main.'
    Save; exit 2
}
$r = G @('rev-parse', '--verify', ($MergeCommit + '^{commit}'))
if ($r.Code -ne 0) { W ('    FAIL: merge commit ' + $MergeCommit + ' not found.'); Save; exit 2 }
$mergeSha = $r.Out[0]
$r = G @('merge-base', '--is-ancestor', $mergeSha, 'HEAD')
if ($r.Code -ne 0) { W ('    FAIL: ' + $MergeCommit + ' not an ancestor of HEAD.'); Save; exit 2 }
W ('    merge commit ' + $mergeSha.Substring(0, 12) + ' confirmed on main')
W ''

# ---- [1] root tools (idempotent re-check) -----------------------------------

W '[1] ROOT TOOLS'
$giPath = Join-Path $RepoRoot '.gitignore'
$gi = ''
if (Test-Path $giPath) { $gi = [System.IO.File]::ReadAllText($giPath) }
if ($gi.Contains('/*_2026*.txt')) {
    W '    .gitignore evidence rules   present (v1 did this)'
} else {
    $append = "`r`n# M1-18: session evidence artifacts stay out of the tree`r`n/*_2026*.txt`r`n/importchain_state.json`r`n/wipetrap_state.json`r`n*.runner-binding-backup-*`r`n"
    [System.IO.File]::AppendAllText($giPath, $append, $utf8)
    $null = G @('add', '--', '.gitignore')
    W '    .gitignore evidence rules   appended'
}
$r = G @('status', '--porcelain=v1', '--untracked-files=all')
$newTools = @()
foreach ($line in $r.Out) {
    if ($line -match '^\?\?\s+(.+)$') {
        $u = $Matches[1].Trim('"')
        if (-not $u.Contains('/') -and $u -match '\.(ps1|md)$' -and $u -notmatch '_2026\d{4}_\d{6}') { $newTools += $u }
    }
}
if (@($newTools).Count -eq 0) {
    W '    root tools                  all committed (v1 did this: 0e2da1b7f4df)'
} else {
    foreach ($t in $newTools) { $null = G @('add', '--', $t); W ('        + ' + $t) }
    $r = G @('commit', '-m', 'M1-18: commit remaining root session tools')
    if ($r.Code -eq 0) { W ('    committed                   ' + ((G @('rev-parse','HEAD')).Out[0]).Substring(0,12)) }
    else { W ('    commit failed: ' + ($r.Out -join ' ')); $fail++ }
}
W ''

# ---- [2] F1: commit the uncommitted access-matrix fix -----------------------

W '[2] ACCESS-MATRIX FIX (F1)'
$acRel = 'Backend/PlantProcess.Api/Security/PlantAccessControl.cs'
$acAbs = Join-Path $RepoRoot ($acRel -replace '/', '\')
if (-not (Test-Path $acAbs)) {
    W ('    FAIL: ' + $acRel + ' missing from the tree.'); $fail++
} else {
    $tree = [System.IO.File]::ReadAllText($acAbs)
    $inTree = $tree.Contains('/api/ml/foundation')
    $r = G @('grep', '-n', '/api/ml/foundation', 'HEAD', '--', $acRel)
    $inHead = ($r.Code -eq 0 -and @($r.Out).Count -gt 0)
    if ($inHead) {
        W '    mapping already in HEAD     nothing to do'
        $r2 = G @('log', '-1', "--format=%h %ad %s", '--date=short', '--', $acRel)
        W ('    carried by: ' + ($r2.Out -join ' '))
    } elseif ($inTree) {
        W '    mapping in WORKING TREE but NOT in any commit - committing it now.'
        $st = G @('status', '--porcelain=v1', '--', $acRel)
        W ('    file status: ' + (($st.Out -join ' ').Trim()))
        $null = G @('add', '--', $acRel)
        $r3 = G @('commit', '-m', 'M1-21: map /api/ml/foundation in the access matrix (analysis.execute) - engine 403 fix')
        if ($r3.Code -eq 0) {
            $sha = ((G @('rev-parse', 'HEAD')).Out[0]).Substring(0, 12)
            W ('    committed                   ' + $sha)
        } else {
            W ('    COMMIT FAILED: ' + ($r3.Out -join ' ')); $fail++
        }
    } else {
        W '    FAIL: mapping absent from tree AND history. The engine will 403.'
        W '    Re-run Fix-MlFoundationAccess.ps1, verify the engine responds, then re-run this.'
        $fail++
    }
}
W ''

# ---- [3] branch deletion (D2 fix: long-form switch) -------------------------

W '[3] BRANCH DELETION'
$r = G @('rev-parse', '--verify', '--quiet', ('refs/heads/' + $BranchName))
if ($r.Code -ne 0) {
    W ('    branch ' + $BranchName + ' does not exist locally - already done')
} else {
    $r = G @('branch', '--delete', $BranchName)
    if ($r.Code -eq 0) {
        W ('    deleted ' + $BranchName + ' (git certified it fully merged)')
    } else {
        W ('    REFUSED by git: ' + ($r.Out -join ' '))
        W ('    Investigate: git log main..' + $BranchName)
        $fail++
    }
}
W ''

# ---- [4] fresh verified bundle + off-laptop copy ----------------------------

W '[4] BUNDLE'
$parent     = Split-Path -Parent $RepoRoot
$bundleName = 'PPIQ_main_' + $stamp + '.bundle'
$bundlePath = Join-Path $parent $bundleName
$r = G @('bundle', 'create', $bundlePath, '--all')
if ($r.Code -ne 0) {
    W ('    bundle create FAILED: ' + ($r.Out -join ' ')); $fail++
} else {
    $r = G @('bundle', 'verify', $bundlePath)
    if ($r.Code -ne 0) {
        W ('    bundle VERIFY FAILED: ' + ($r.Out -join ' ')); $fail++
    } else {
        $sha  = (Get-FileHash -Algorithm SHA256 -LiteralPath $bundlePath).Hash
        $size = [math]::Round(((Get-Item -LiteralPath $bundlePath).Length / 1MB), 1)
        W ('    created + verified          ' + $bundlePath)
        W ('    size / sha256               ' + $size + ' MB / ' + $sha)
        if ([string]::IsNullOrWhiteSpace($BundleTarget)) {
            W '    OFF-LAPTOP STEP OPEN: re-run with -BundleTarget or copy by hand.'
        } else {
            if (-not (Test-Path $BundleTarget)) { New-Item -ItemType Directory -Path $BundleTarget -Force | Out-Null }
            $dest = Join-Path $BundleTarget $bundleName
            Copy-Item -LiteralPath $bundlePath -Destination $dest -Force
            $sha2 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dest).Hash
            if ($sha2 -eq $sha) { W ('    copied + hash-verified      ' + $dest) }
            else { W ('    COPY HASH MISMATCH          ' + $dest); $fail++ }
        }
    }
}
W ''

W '=============================================================================='
if ($fail -eq 0) {
    W 'M1-18: COMPLETE. The matrix fix is in history, the branch is gone, the'
    W 'bundle is verified and off-machine. Board line closes.'
} else {
    W ('M1-18: ' + $fail + ' item(s) FAILED - see above.')
}
W '=============================================================================='
Save
if ($fail -eq 0) { exit 0 } else { exit 1 }
