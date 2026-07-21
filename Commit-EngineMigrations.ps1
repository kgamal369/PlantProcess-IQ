<#
.SYNOPSIS
    Commit-EngineMigrations.ps1 - closes M1-31. Moves migrations 741 (+740 note)
    and 742 into Backend\database\scripts\, patches Rebuild-PresentationDb.ps1 to
    re-apply them after every restore (so a rebuild never re-blinds the engine),
    then git-commits everything. Full contract: preflight -> backup ->
    move+patch -> self-check -> commit -> verify.

.DESCRIPTION
    Rebuild-PresentationDb restores from a pg_dump fixture that PREDATES the
    engine fixes. Without this, running it tomorrow silently loses the lineage
    view, the coil-grain projection, and the defect outcomes. This inserts a new
    step "[1b] engine migrations" right after the restore that runs 741 then 742
    from the committed scripts folder.

.PARAMETER NoCommit  do everything except the git commit (inspect first)
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = (Get-Location).Path,
    [switch]$NoCommit
)

$ErrorActionPreference = 'Continue'
Set-StrictMode -Version Latest
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("Commit_EngineMigrations_" + $stamp + ".txt")
$lines = New-Object System.Collections.Generic.List[string]
$utf8 = New-Object System.Text.UTF8Encoding($false)
function W([string]$t=''){ $lines.Add($t); Write-Host $t }
function Save { [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n")+"`r`n"), $utf8); Write-Host ''; Write-Host ('Log: '+$logPath) -ForegroundColor Cyan }

# git on PATH
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { $env:Path += ';C:\Program Files\Git\cmd' }
function Git { $o = & git @args 2>&1; return @{ code=$LASTEXITCODE; out=$o } }

W '=============================================================================='
W ('COMMIT ENGINE MIGRATIONS (M1-31) - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''

# ---- preflight -------------------------------------------------------------
W '[PREFLIGHT]'
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { W '  FAIL: git not on PATH even after adding Git\cmd. Fix PATH and retry.'; Save; exit 2 }
$scriptsDir = Join-Path $RepoRoot 'Backend\database\scripts'
$rebuild    = Join-Path $RepoRoot 'scripts\demo\Rebuild-PresentationDb.ps1'
foreach ($p in @($scriptsDir, $rebuild)) {
    if (Test-Path -LiteralPath $p) { W ('  found  ' + $p) } else { W ('  MISSING ' + $p); Save; exit 2 }
}
# the loose migration files at repo root
$src741 = Join-Path $RepoRoot '741_feature_store_coil_grain_projection.sql'
$src742 = Join-Path $RepoRoot '742_feature_regrain_generic.sql'
$src740 = Join-Path $RepoRoot '740_feature_store_heat_lineage_and_defect_outcomes.sql'
foreach ($p in @($src741,$src742)) {
    if (-not (Test-Path -LiteralPath $p)) { W ('  MISSING migration at root: ' + $p + ' (download it from chat first)'); Save; exit 2 }
}
W ''

# ---- move migrations into scripts/ -----------------------------------------
W '[MOVE] migrations -> Backend\database\scripts\'
Copy-Item -LiteralPath $src741 -Destination (Join-Path $scriptsDir '741_feature_store_coil_grain_projection.sql') -Force; W '  741 -> scripts/'
Copy-Item -LiteralPath $src742 -Destination (Join-Path $scriptsDir '742_feature_regrain_generic.sql') -Force; W '  742 -> scripts/'
if (Test-Path -LiteralPath $src740) { Copy-Item -LiteralPath $src740 -Destination (Join-Path $scriptsDir '740_feature_store_heat_lineage_and_defect_outcomes.sql') -Force; W '  740 -> scripts/ (superseded by 741; kept for history)' }
W ''

# ---- patch Rebuild-PresentationDb ------------------------------------------
W '[PATCH] insert step [1b] engine migrations after restore'
$rb = [System.IO.File]::ReadAllText($rebuild)
if ($rb.Contains('engine migrations 741+742')) {
    W '  already patched - skipping'
} else {
    $anchor = '# ---- 2. Rule-1 fixes -------------------------------------------------------'
    if (-not $rb.Contains($anchor)) { W '  FAIL: anchor (step 2 header) not found; not patching blind.'; Save; exit 2 }
    $bak = $rebuild + '.' + $stamp + '.bak'
    Copy-Item -LiteralPath $rebuild -Destination $bak -Force; W ('  backup: ' + $bak)
    $inject = @'
# ---- 1b. engine migrations (heat lineage + coil projection + defect outcomes)
W "[1b/7] engine migrations 741+742 (or the engine re-blinds on every rebuild)"
foreach ($mig in @('741_feature_store_coil_grain_projection.sql','742_feature_regrain_generic.sql')) {
    $migPath = Join-Path $PSScriptRoot ('..\..\Backend\database\scripts\' + $mig)
    if (Test-Path -LiteralPath $migPath) {
        $mo = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -v ON_ERROR_STOP=1 -X -q -1 -f $migPath 2>&1
        if ($LASTEXITCODE -eq 0) { W ("      applied " + $mig) } else { W ("      FAILED " + $mig + ": " + ($mo -join ' ')) }
    } else { W ("      MISSING " + $migPath) }
}
W ("      lineage view rows: " + (Q1 "SELECT COUNT(*) FROM ppiq_ml_unit_heat_lineage;"))
W ""

'@
    $rb = $rb.Replace($anchor, $inject + $anchor)
    [System.IO.File]::WriteAllText($rebuild, $rb, $utf8)
    $verify = [System.IO.File]::ReadAllText($rebuild)
    if (-not $verify.Contains('engine migrations 741+742')) { W '  FAIL: marker not on disk; restoring.'; Copy-Item $bak $rebuild -Force; Save; exit 1 }
    W '  inserted step [1b] (verified on disk)'
}
W ''

# ---- self-check ------------------------------------------------------------
W '[SELF-CHECK]'
$ok1 = Test-Path -LiteralPath (Join-Path $scriptsDir '741_feature_store_coil_grain_projection.sql')
$ok2 = Test-Path -LiteralPath (Join-Path $scriptsDir '742_feature_regrain_generic.sql')
$ok3 = ([System.IO.File]::ReadAllText($rebuild)).Contains('engine migrations 741+742')
W ('  741 in scripts/: ' + $ok1)
W ('  742 in scripts/: ' + $ok2)
W ('  rebuild patched: ' + $ok3)
if (-not ($ok1 -and $ok2 -and $ok3)) { W '  FAIL: self-check failed.'; Save; exit 1 }
W ''

# ---- commit ----------------------------------------------------------------
if ($NoCommit) { W '[COMMIT] skipped (-NoCommit). Inspect, then: git add -A; git commit'; Save; exit 0 }
W '[COMMIT] git add -A + commit (also captures ALL other uncommitted demo-eve work)'
$r = Git add -A
$r = Git commit -m 'engine: heat lineage + coil projection + defect outcomes (740/741/742) folded into rebuild; demo-eve fixes'
foreach ($l in $r.out) { W ('  ' + $l) }
$sha = (Git rev-parse --short HEAD).out
W ('  HEAD: ' + $sha)
W ''
W '[VERIFY]'
$tracked = (Git ls-files 'Backend/database/scripts/741_feature_store_coil_grain_projection.sql').out
W ('  741 tracked: ' + ($tracked -join ''))
W ''
W 'DONE (M1-31). The engine fixes now survive Rebuild-PresentationDb.'
W 'Record the HEAD sha in the backlog. Next: run M1-18 finish to re-cut the bundle.'
Save
exit 0
