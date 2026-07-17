<#
.SYNOPSIS
    Complete-M1-18.ps1 - finishes the merge task: root-tools commit, evidence-log
    ignore rules, matrix-fix commit verification, presentation branch deletion,
    fresh verified bundle, off-laptop copy.

.DESCRIPTION
    Remaining items on the M1-18 board line:
      1. amend msg            -> HANDLED WITH A GUARD (see -AmendMessage; default SKIP,
                                 and the script refuses when amending would invalidate
                                 the existing tag/bundle or rewrite pushed history)
      2. branch -d            -> done here (safe -d only, never -D)
      3. commit root tools    -> done here (tool scripts + walk-evidence.md committed;
                                 timestamped evidence logs and state JSONs ignored)
      4. verify matrix-fix    -> done here (proves /api/ml/foundation mapping is in a
                                 commit on HEAD, names the commit)
      5. bundle off laptop    -> done here (fresh --all bundle, verified, SHA256,
                                 copied to -BundleTarget when provided)

    Read-only toward the database. Git-state changes only: one commit, one branch
    deletion (merged-only), gitignore append. Every action is logged with the SHA
    it produced so it can be undone by hand if ever needed.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-18.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-18.ps1 -BundleTarget "E:\PPIQ-Archive"

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Complete-M1-18.ps1 -AmendMessage "M1-18: merge presentation into main (workspace resurrection + mojibake fix + presentation profile)"
#>

[CmdletBinding()]
param(
    [string]$RepoRoot     = (Get-Location).Path,
    [string]$MergeCommit  = 'ab9771f9',
    [string]$BranchName   = 'presentation',
    [string]$BundleTarget = '',
    [string]$AmendMessage = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("M1-18_Complete_" + $stamp + ".txt")
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
W ('M1-18 COMPLETION - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('Repo: ' + $RepoRoot)
W '=============================================================================='
W ''

# ---- [0] preflight ----------------------------------------------------------

Set-Location $RepoRoot
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) {
    W 'FAIL: not a git repository root.'
    Save; exit 2
}
$r = G rev-parse --abbrev-ref HEAD
if ($r.Code -ne 0) { W 'FAIL: git unavailable.'; Save; exit 2 }
$branch = $r.Out[0]
W ('[0] PREFLIGHT   branch=' + $branch)
if ($branch -ne 'main') {
    W '    FAIL: not on main. M1-18 completion runs on the merged trunk only.'
    Save; exit 2
}
$r = G rev-parse --verify ($MergeCommit + '^{commit}')
if ($r.Code -ne 0) {
    W ('    FAIL: merge commit ' + $MergeCommit + ' not found.')
    Save; exit 2
}
$mergeSha = $r.Out[0]
$r = G merge-base --is-ancestor $mergeSha HEAD
if ($r.Code -ne 0) {
    W ('    FAIL: ' + $MergeCommit + ' is not an ancestor of HEAD.')
    Save; exit 2
}
W ('    merge commit ' + $mergeSha.Substring(0, 12) + ' confirmed on main')
W ''

# ---- [1] amend guard --------------------------------------------------------

W '[1] AMEND MESSAGE'
if ([string]::IsNullOrWhiteSpace($AmendMessage)) {
    W '    skipped (no -AmendMessage given). RECOMMENDED: the commit is already'
    W '    tagged and bundled; amending would change its SHA and invalidate both.'
} else {
    $headSha = (G rev-parse HEAD).Out[0]
    $tags    = (G tag --points-at $mergeSha).Out | Where-Object { $_ -ne '' }
    $remotes = (G branch -r --contains $mergeSha).Out | Where-Object { $_ -ne '' }
    $blockers = New-Object System.Collections.Generic.List[string]
    if ($headSha -ne $mergeSha) { $blockers.Add('commit is not HEAD (amend would rewrite intermediate history)') }
    if (@($tags).Count -gt 0) { $blockers.Add('tag(s) point at it: ' + (@($tags) -join ', ')) }
    if (@($remotes).Count -gt 0) { $blockers.Add('remote branch(es) contain it: ' + (@($remotes) -join ', ')) }
    if ($blockers.Count -gt 0) {
        W '    REFUSED. Amending this commit would be destructive:'
        foreach ($b in $blockers) { W ('      - ' + $b) }
        W '    The message stands. History is worth more than a prettier sentence.'
    } else {
        $r = G commit --amend -m $AmendMessage
        if ($r.Code -eq 0) {
            $newSha = (G rev-parse HEAD).Out[0]
            W ('    amended. New SHA: ' + $newSha.Substring(0, 12))
            $mergeSha = $newSha
        } else {
            W ('    amend failed: ' + ($r.Out -join ' ')); $fail++
        }
    }
}
W ''

# ---- [2] ignore evidence artifacts, commit root tools ----------------------

W '[2] ROOT TOOLS COMMIT'

$ignoreBlock = @(
    '# M1-18: session evidence artifacts stay out of the tree',
    '/*_2026*.txt',
    '/importchain_state.json',
    '/wipetrap_state.json',
    '*.runner-binding-backup-*'
)
$giPath = Join-Path $RepoRoot '.gitignore'
$gi = ''
if (Test-Path $giPath) { $gi = [System.IO.File]::ReadAllText($giPath) }
if ($gi.Contains('/*_2026*.txt')) {
    W '    .gitignore evidence rules   already present'
} else {
    $append = "`r`n" + (($ignoreBlock) -join "`r`n") + "`r`n"
    [System.IO.File]::AppendAllText($giPath, $append, $utf8)
    W '    .gitignore evidence rules   appended (4 patterns)'
}

$r = G status --porcelain=v1 --untracked-files=all
$untracked = @()
foreach ($line in $r.Out) {
    if ($line -match '^\?\?\s+(.+)$') { $untracked += $Matches[1].Trim('"') }
}
$rootTools = @()
$skipped   = @()
foreach ($u in $untracked) {
    if ($u.Contains('/')) { continue }                         # root only
    if ($u -match '_2026\d{4}_\d{6}\.txt$') { $skipped += $u; continue }
    if ($u -match '_state\.json$') { $skipped += $u; continue }
    if ($u -match 'runner-binding-backup') { $skipped += $u; continue }
    if ($u -match '\.(ps1|md)$') { $rootTools += $u; continue }
    $skipped += $u
}
W ('    tool files to commit        ' + @($rootTools).Count)
foreach ($t in $rootTools) { W ('        + ' + $t) }
W ('    evidence/state ignored      ' + @($skipped).Count + ' (not committed, per .gitignore)')

if (@($rootTools).Count -gt 0 -or -not $gi.Contains('/*_2026*.txt')) {
    $null = G add -- .gitignore
    foreach ($t in $rootTools) { $null = G add -- $t }
    $r = G commit -m 'M1-18: commit root session tools; ignore evidence artifacts'
    if ($r.Code -eq 0) {
        $toolsSha = (G rev-parse HEAD).Out[0]
        W ('    committed                   ' + $toolsSha.Substring(0, 12))
    } else {
        $staged = (G diff --cached --name-only).Out | Where-Object { $_ -ne '' }
        if (@($staged).Count -eq 0) {
            W '    nothing to commit           (already clean)'
        } else {
            W ('    COMMIT FAILED: ' + ($r.Out -join ' ')); $fail++
        }
    }
} else {
    W '    nothing to commit           (already clean)'
}
W ''

# ---- [3] verify the matrix-fix commit --------------------------------------

W '[3] MATRIX-FIX VERIFICATION'
$acPath = 'Backend/PlantProcess.Api/Security/PlantAccessControl.cs'
$r = G grep -n '/api/ml/foundation' HEAD -- $acPath
if ($r.Code -eq 0 -and @($r.Out).Count -gt 0) {
    W '    /api/ml/foundation IS mapped in HEAD:'
    foreach ($hit in ($r.Out | Select-Object -First 3)) { W ('        ' + $hit) }
    $r2 = G log -1 --format='%h %ad %s' --date=short -- $acPath
    W ('    last commit touching the matrix: ' + ($r2.Out -join ' '))
} else {
    W '    FAIL: /api/ml/foundation NOT found in HEAD PlantAccessControl.cs.'
    W '    The access-matrix fix is uncommitted or lost. Do not proceed to rehearsal'
    W '    until this is in a commit - the engine dies with 403 without it.'
    $fail++
}
W ''

# ---- [4] delete the presentation branch (safe) ------------------------------

W '[4] BRANCH DELETION'
$r = G rev-parse --verify --quiet ('refs/heads/' + $BranchName)
if ($r.Code -ne 0) {
    W ('    branch ' + $BranchName + ' does not exist locally - already done')
} else {
    $r = G branch -d $BranchName
    if ($r.Code -eq 0) {
        W ('    deleted ' + $BranchName + ' (-d: git itself certified it fully merged)')
    } else {
        W ('    REFUSED by git (-d only deletes merged branches): ' + ($r.Out -join ' '))
        W '    If this branch holds unmerged commits, that is a finding, not an obstacle'
        W '    to force through. Investigate with: git log main..' + $BranchName
        $fail++
    }
}
W ''

# ---- [5] fresh verified bundle + off-laptop copy ----------------------------

W '[5] BUNDLE'
$parent     = Split-Path -Parent $RepoRoot
$bundleName = 'PPIQ_main_' + $stamp + '.bundle'
$bundlePath = Join-Path $parent $bundleName
$r = G bundle create $bundlePath --all
if ($r.Code -ne 0) {
    W ('    bundle create FAILED: ' + ($r.Out -join ' ')); $fail++
} else {
    $r = G bundle verify $bundlePath
    if ($r.Code -ne 0) {
        W ('    bundle VERIFY FAILED: ' + ($r.Out -join ' ')); $fail++
    } else {
        $sha  = (Get-FileHash -Algorithm SHA256 -LiteralPath $bundlePath).Hash
        $size = [math]::Round(((Get-Item -LiteralPath $bundlePath).Length / 1MB), 1)
        W ('    created + verified          ' + $bundlePath)
        W ('    size                        ' + $size + ' MB')
        W ('    sha256                      ' + $sha)
        if ([string]::IsNullOrWhiteSpace($BundleTarget)) {
            W ''
            W '    OFF-LAPTOP STEP STILL OPEN: no -BundleTarget given. Copy the bundle'
            W '    to a drive or cloud folder that is not this machine, or re-run with'
            W '    -BundleTarget "E:\somewhere". M1-12 continuity requires it off-machine.'
        } else {
            if (-not (Test-Path $BundleTarget)) { New-Item -ItemType Directory -Path $BundleTarget -Force | Out-Null }
            $dest = Join-Path $BundleTarget $bundleName
            Copy-Item -LiteralPath $bundlePath -Destination $dest -Force
            $sha2 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dest).Hash
            if ($sha2 -eq $sha) {
                W ('    copied + hash-verified      ' + $dest)
            } else {
                W ('    COPY HASH MISMATCH at ' + $dest + ' - copy again.'); $fail++
            }
        }
    }
}
W ''

# ---- verdict ----------------------------------------------------------------

W '=============================================================================='
if ($fail -eq 0) {
    W 'M1-18: COMPLETE. Evidence above; keep this log.'
} else {
    W ('M1-18: ' + $fail + ' item(s) FAILED - see above. The board line stays partial.')
}
W '=============================================================================='
Save
if ($fail -eq 0) { exit 0 } else { exit 1 }
