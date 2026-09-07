# Close-PlantVocabularyDataAuthority.ps1
#
# T-093  Plant-vocabulary sweep, part 1: build the term list and the architecture test.
# Owner  Worker 1. Release M2-P1. Final closure pack, one invocation.
#
# What this pack does, in order:
#   0  repository / HEAD / index / foreign-dirt fingerprint
#   1  inspect the exact T-093 owned paths
#   2  identify failed-candidate residue by content marker (never by guess)
#   3  recover exact owned preimages from HEAD, cross-checked against the r1 backup
#   4  prove the pristine owned baseline
#   5  classify the JobLog focused test on the pristine tree
#   6  apply the corrected implementation (manifest printed before mutation)
#   7  compile; UA-08 positive AND negative falsification; system-template shared DATA
#   8  authoritative candidate measurement: the SAME generator, temporary sink outside repo
#   9  generate the real baseline and inventory with the SAME generator
#  10  mechanical ratchet guards, including candidate == generated
#  11  JobLog focused test after apply; before/after classification
#  12  adjacent architecture regression, once
#  13  exact staging, cached-diff check
#  14  commit
#  15  post-commit closure proof
#
# Native processes are always invoked through a wrapper that isolates PowerShell 5.1's
# stderr-to-terminating-error behaviour. The exit code is the contract. Rollback of the
# owned paths runs on every pre-commit failure.
#
# Exit codes: 0 CLOSED GREEN. 2 refused before any mutation. 3 failed, owned paths
#             restored. 4 commit failed, owned paths left staged for inspection.

[CmdletBinding()]
param(
    [string] $RepoRoot    = 'C:\Workspace\PlantProcess-IQ',
    [string] $BackupRoot  = 'C:\Workspace\_ppiq_backups',
    [string] $EvidenceDir = 'C:\Workspace\_ppiq_evidence',
    [string] $PriorBackup = 'C:\Workspace\_ppiq_backups\t093_20260902_022747',
    [string] $ExpectedParent = '7cad36e4838112f00511af51b0169a0bddbccf00'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$Stamp     = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $BackupRoot ("t093_final_" + $Stamp)
$CandidateDir = Join-Path $EvidenceDir ("t093_candidate_" + $Stamp)
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Write-Step { param([string] $Text) Write-Host ''; Write-Host ("=== " + $Text + " ===") }
function Write-Ok   { param([string] $Text) Write-Host ("  [ok]   " + $Text) }
function Write-Warn { param([string] $Text) Write-Host ("  [warn] " + $Text) }
function Write-Bad  { param([string] $Text) Write-Host ("  [BAD]  " + $Text) }

function Stop-Refused {
    param([string] $Reason)
    Write-Bad ("REFUSED BEFORE MUTATION: " + $Reason)
    exit 2
}

# ---------------------------------------------------------------------------
# NATIVE CALL ISOLATION
# ---------------------------------------------------------------------------
$Script:LastNativeExit = 0

function Invoke-NativeLogged {
    param([string] $Exe, [string[]] $NativeArgs, [string] $LogPath)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        # Out-Host at the end: Tee-Object would otherwise forward every console line into
        # this function's OUTPUT stream, and the caller's "$exit = Invoke-NativeLogged"
        # would receive the whole log plus the exit code as one array.
        & $Exe @NativeArgs 2>&1 | ForEach-Object { $_.ToString() } | Tee-Object -FilePath $LogPath | Out-Host
        $Script:LastNativeExit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
    }
    return $Script:LastNativeExit
}

function Invoke-NativeLines {
    param([string] $Exe, [string[]] $NativeArgs)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $captured = @()
    try {
        # STDOUT only. This function's result is PARSED (status lines, SHAs, path lists), and
        # git writes advisory text such as "warning: CRLF will be replaced by LF" to stderr;
        # merging it in would turn a clean path into a dirty one. The exit code still rules.
        $captured = @(& $Exe @NativeArgs 2>$null)
        $Script:LastNativeExit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
    }
    # Callers ALWAYS wrap this in @(...). PowerShell unrolls a single-element array on
    # return; a unary-comma return would instead nest the array when piped. The @() at
    # the call site is the one form that is correct for zero, one and many lines.
    return @($captured | ForEach-Object { $_.ToString() })
}

function Get-Sha256 {
    param([string] $Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-NormalisedTextSha256 {
    param([string] $Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $text = (New-Object System.Text.UTF8Encoding($false)).GetString($bytes)
    if ($text.Length -gt 0 -and [int]$text[0] -eq 0xFEFF) { $text = $text.Substring(1) }
    $text = $text -replace "`r`n", "`n"
    return Get-TextSha256 -Text $text
}

function Get-TextSha256 {
    param([string] $Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Text))
    return ([System.BitConverter]::ToString($bytes) -replace '-', '')
}

function Write-AsciiFile {
    param([string] $Path, [string] $Content, [string] $Eol = 'CRLF')
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $n = $Content -replace "`r`n", "`n"
    if ($Eol -eq 'CRLF') { $n = $n -replace "`n", "`r`n" }
    [System.IO.File]::WriteAllText($Path, $n, $Utf8NoBom)
}

# An edited file keeps the line endings it had when read. Git normalises on add either
# way; preserving the on-disk EOL keeps the working tree free of whole-file EOL churn.
function Get-FileEol {
    param([string] $Text)
    if ($Text.Contains("`r`n")) { return 'CRLF' }
    return 'LF'
}

function Assert-Ascii {
    param([string] $Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    foreach ($b in $bytes) { if ($b -gt 127) { Invoke-Rollback ("non-ASCII byte written into " + $Path) } }
}

# ---------------------------------------------------------------------------
# OWNED MANIFEST
# ---------------------------------------------------------------------------
$Script:OwnedPaths = @(
    'Backend/tests/PlantProcess.Architecture.Tests/plant_vocabulary_terms.json',
    'Backend/tests/PlantProcess.Architecture.Tests/ScopeAwareGenericity.cs',
    'Backend/tests/PlantProcess.Architecture.Tests/ScopeAwareGenericityRuleTests.cs',
    'Backend/tests/PlantProcess.Architecture.Tests/GenericityBaselineGateTests.cs',
    'Backend/tests/PlantProcess.Architecture.Tests/SystemTemplateAuthorityTests.cs',
    'Backend/tests/PlantProcess.Architecture.Tests/genericity_violation_baseline.json',
    'docs/quality/GenericityViolationInventory.md'
)

$Script:OwnedReasons = @{
    'Backend/tests/PlantProcess.Architecture.Tests/plant_vocabulary_terms.json'        = 'NEW. The one DATA authority (R1).'
    'Backend/tests/PlantProcess.Architecture.Tests/ScopeAwareGenericity.cs'            = 'UA-08 semantic rule built from DATA; derivative scope; authority loader (R2).'
    'Backend/tests/PlantProcess.Architecture.Tests/ScopeAwareGenericityRuleTests.cs'   = 'UA-08 positive and negative falsification; data-driven proofs.'
    'Backend/tests/PlantProcess.Architecture.Tests/GenericityBaselineGateTests.cs'     = 'Bounded: the existing generator gains an output-directory sink so candidate measurement uses the SAME generator (R3).'
    'Backend/tests/PlantProcess.Architecture.Tests/SystemTemplateAuthorityTests.cs'    = 'Compiled vocabulary array replaced by the shared DATA projection (R4).'
    'Backend/tests/PlantProcess.Architecture.Tests/genericity_violation_baseline.json' = 'Regenerated ratchet: 49 preserved + UA-08 grandfathered.'
    'docs/quality/GenericityViolationInventory.md'                                     = 'Regenerated inventory, handed to T-094.'
}

$TestDir      = 'Backend\tests\PlantProcess.Architecture.Tests'
$TestProj     = Join-Path $RepoRoot ($TestDir + '\PlantProcess.Architecture.Tests.csproj')
$ScannerFile  = Join-Path $RepoRoot ($TestDir + '\ScopeAwareGenericity.cs')
$RuleTestFile = Join-Path $RepoRoot ($TestDir + '\ScopeAwareGenericityRuleTests.cs')
$GateTestFile = Join-Path $RepoRoot ($TestDir + '\GenericityBaselineGateTests.cs')
$SysTestFile  = Join-Path $RepoRoot ($TestDir + '\SystemTemplateAuthorityTests.cs')
$VocabFile    = Join-Path $RepoRoot ($TestDir + '\plant_vocabulary_terms.json')
$BaselineFile = Join-Path $RepoRoot ($TestDir + '\genericity_violation_baseline.json')
$InventoryFile = Join-Path $RepoRoot 'docs\quality\GenericityViolationInventory.md'
$VocabRel     = 'Backend/tests/PlantProcess.Architecture.Tests/plant_vocabulary_terms.json'

function ConvertTo-Live { param([string] $Rel) return (Join-Path $RepoRoot ($Rel -replace '/', '\')) }
function ConvertTo-BackupName { param([string] $Rel) return ($Rel -replace '/', '__') }

# ---------------------------------------------------------------------------
# ROLLBACK (owned paths only, foreign dirt untouched)
# ---------------------------------------------------------------------------
$Script:AppliedAnything = $false

# Safety net: ANY unhandled terminating error after apply must still roll the owned
# paths back. r1 died from an unhandled error and left the candidate on disk.
trap {
    Invoke-Rollback ("unhandled error: " + $_.Exception.Message + " at line " + $_.InvocationInfo.ScriptLineNumber)
}

function Invoke-Rollback {
    param([string] $Reason)

    Write-Bad ("FAILURE: " + $Reason)

    if ($Script:AppliedAnything) {
        Write-Step 'AUTOMATIC ROLLBACK of owned paths'
        $null = Invoke-NativeLines -Exe 'git' -NativeArgs @('reset', '-q')

        foreach ($rel in $Script:OwnedPaths) {
            $live = ConvertTo-Live $rel
            $backup = Join-Path $BackupDir (ConvertTo-BackupName $rel)

            if (Test-Path -LiteralPath $backup) {
                Copy-Item -LiteralPath $backup -Destination $live -Force
                Write-Ok ("restored " + $rel)
            } elseif (Test-Path -LiteralPath $live) {
                Remove-Item -LiteralPath $live -Force
                Write-Ok ("removed created " + $rel)
            }
        }

        $post = @(Invoke-NativeLines -Exe 'git' -NativeArgs (@('status', '--porcelain', '--') + $Script:OwnedPaths))
        $head = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('rev-parse', 'HEAD'))[0].Trim()
        Write-Ok ("owned paths dirty after rollback : " + @($post | Where-Object { $_ -ne '' }).Count)
        Write-Ok ("HEAD after rollback              : " + $head)
        Write-Ok ("foreign fingerprint after        : " + (Get-ForeignFingerprint))
    } else {
        Write-Ok 'nothing had been applied; no rollback needed'
    }

    exit 3
}

function Get-ForeignEntries {
    $all = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('status', '--porcelain') | Where-Object { $_ -ne '' })
    $foreign = @()
    foreach ($line in $all) {
        $path = $line.Substring(3).Trim()
        if ($path -like '* -> *') { $path = ($path -split ' -> ')[1] }
        $path = $path -replace '\\', '/'
        if ($Script:OwnedPaths -notcontains $path) { $foreign += $line }
    }
    return @($foreign | Sort-Object)
}

function Get-ForeignFingerprint {
    $entries = @(Get-ForeignEntries)
    return (Get-TextSha256 -Text ($entries -join "`n")).Substring(0, 16) + " (" + $entries.Count + " entries)"
}

function Invoke-AnchoredReplace {
    param([string] $Path, [string] $Old, [string] $New, [string] $Label)

    if (-not (Test-Path -LiteralPath $Path)) { Invoke-Rollback ("anchored replace: missing file " + $Path) }

    $original = [System.IO.File]::ReadAllText($Path)
    $eol = Get-FileEol $original
    $lf = $original -replace "`r`n", "`n"
    $o  = $Old -replace "`r`n", "`n"
    $n  = $New -replace "`r`n", "`n"

    $count = 0; $idx = 0
    while ($true) {
        $idx = $lf.IndexOf($o, $idx, [System.StringComparison]::Ordinal)
        if ($idx -lt 0) { break }
        $count++; $idx += $o.Length
    }
    if ($count -ne 1) { Invoke-Rollback ("anchored replace '" + $Label + "': anchor matched " + $count + " times in " + $Path) }

    Write-AsciiFile -Path $Path -Content ($lf.Replace($o, $n)) -Eol $eol
    Write-Ok ("applied: " + $Label)
}

# Returns the fully qualified NAMES of failing tests, not the raw console lines.
#
# xUnit prefixes each [FAIL] line with an elapsed timestamp - "[xUnit.net 00:00:00.52]" -
# which differs on every run. Comparing raw lines across two runs therefore reports a
# difference that is only a clock reading. The identity of a failure is its test name.
function Get-FailLines {
    param([string] $LogPath)
    if (-not (Test-Path -LiteralPath $LogPath)) { return @() }

    $names = @()
    foreach ($line in (Get-Content -LiteralPath $LogPath)) {
        $m = [regex]::Match($line, '^\s*(?:\[xUnit\.net[^\]]*\]\s*)?(.+?)\s*\[FAIL\]\s*$')
        if ($m.Success) { $names += $m.Groups[1].Value.Trim() }
    }
    return @($names | Sort-Object -Unique)
}

# ===========================================================================
# PHASE 0 - REPOSITORY / HEAD / INDEX / FOREIGN FINGERPRINT
# ===========================================================================
Write-Step 'Phase 0 - repository state'

if (-not (Test-Path -LiteralPath $RepoRoot)) { Stop-Refused ("repository root not found: " + $RepoRoot) }
Set-Location -LiteralPath $RepoRoot

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) { Stop-Refused 'dotnet is not on PATH' }
$gitCmd = Get-Command git -ErrorAction SilentlyContinue
if ($null -eq $gitCmd) { Stop-Refused 'git is not on PATH' }

$HeadBefore = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('rev-parse', 'HEAD'))[0].Trim()
$Branch     = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('rev-parse', '--abbrev-ref', 'HEAD'))[0].Trim()
Write-Ok ("HEAD   : " + $HeadBefore)
Write-Ok ("branch : " + $Branch)

if ($HeadBefore -ne $ExpectedParent) {
    Stop-Refused ("HEAD is " + $HeadBefore + " but the failed candidate began from " + $ExpectedParent + ". Recovery-from-HEAD is only valid on that exact parent. Re-measure before proceeding.")
}
Write-Ok 'HEAD is the exact parent the failed candidate started from'

$IndexBefore = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('diff', '--cached', '--name-only') | Where-Object { $_ -ne '' })
if ($IndexBefore.Count -ne 0) { Stop-Refused ("index is not empty (" + $IndexBefore.Count + " staged paths)") }
Write-Ok 'index is empty'

$ForeignEntriesBefore = @(Get-ForeignEntries)
$ForeignBefore = Get-ForeignFingerprint
Write-Ok ("foreign fingerprint : " + $ForeignBefore)
foreach ($f in $ForeignEntriesBefore) { Write-Host ("         " + $f) }

foreach ($d in @($EvidenceDir, $BackupDir, $CandidateDir)) {
    if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}

# ===========================================================================
# PHASE 1 - INSPECT OWNED PATHS
# ===========================================================================
Write-Step 'Phase 1 - owned paths'

$OwnedStatus = @(Invoke-NativeLines -Exe 'git' -NativeArgs (@('status', '--porcelain', '--') + $Script:OwnedPaths) | Where-Object { $_ -ne '' })
foreach ($s in $OwnedStatus) { Write-Host ("         " + $s) }
if ($OwnedStatus.Count -eq 0) { Write-Ok 'owned paths are clean' } else { Write-Warn ("owned paths dirty: " + $OwnedStatus.Count) }

# ===========================================================================
# PHASE 2 - IDENTIFY FAILED-CANDIDATE RESIDUE BY CONTENT MARKER
# ===========================================================================
Write-Step 'Phase 2 - residue identification'

$Markers = @{
    'Backend/tests/PlantProcess.Architecture.Tests/ScopeAwareGenericity.cs'            = 'PlantVocabularyAuthority'
    'Backend/tests/PlantProcess.Architecture.Tests/ScopeAwareGenericityRuleTests.cs'   = 'UA-08'
    'Backend/tests/PlantProcess.Architecture.Tests/GenericityBaselineGateTests.cs'     = 'PPIQ_GENERICITY_OUTPUT_DIR'
    'Backend/tests/PlantProcess.Architecture.Tests/SystemTemplateAuthorityTests.cs'    = 'PlantVocabularyAuthority'
    'Backend/tests/PlantProcess.Architecture.Tests/genericity_violation_baseline.json' = '"UA-08"'
    'docs/quality/GenericityViolationInventory.md'                                     = '[UA-08]'
}

$Residue = @()
foreach ($line in $OwnedStatus) {
    $path = ($line.Substring(3).Trim()) -replace '\\', '/'
    $code = $line.Substring(0, 2)
    $live = ConvertTo-Live $path

    if ($path -eq $VocabRel) {
        if ($code -ne '??') { Stop-Refused ("vocabulary DATA path has status '" + $code + "', expected untracked-new") }
        $txt = Get-Content -LiteralPath $live -Raw
        if ($txt -notmatch '"task":\s*"T-093"') { Stop-Refused ("untracked " + $path + " does not carry the T-093 marker; not proven to be the failed pack's file. STOP.") }
        $Residue += $path
        Write-Ok ("residue proven (marker T-093)          : " + $path)
        continue
    }

    if (-not $Markers.ContainsKey($path)) { Stop-Refused ("dirty owned path without a residue marker rule: " + $path) }
    $txt = Get-Content -LiteralPath $live -Raw
    if ($txt.IndexOf($Markers[$path], [System.StringComparison]::Ordinal) -lt 0) {
        Stop-Refused ("dirty " + $path + " does not contain the failed-candidate marker '" + $Markers[$path] + "'. This is not proven to be T-093 residue. STOP; do not restore blindly.")
    }
    $Residue += $path
    Write-Ok ("residue proven (marker " + $Markers[$path] + ") : " + $path)
}
if ($Residue.Count -eq 0) { Write-Ok 'no residue' }

# ===========================================================================
# PHASE 3 - RECOVER EXACT OWNED PREIMAGES
# ===========================================================================
Write-Step 'Phase 3 - recovery'

foreach ($path in $Residue) {
    $live = ConvertTo-Live $path

    if ($path -eq $VocabRel) {
        Remove-Item -LiteralPath $live -Force
        Write-Ok ("removed failed-pack file " + $path)
        continue
    }

    $null = Invoke-NativeLines -Exe 'git' -NativeArgs @('checkout', '--', $path)
    if ($Script:LastNativeExit -ne 0) { Stop-Refused ("git checkout failed for " + $path) }

    $backup = Join-Path $PriorBackup (ConvertTo-BackupName $path)
    if (Test-Path -LiteralPath $backup) {
        $hLive = Get-Sha256 $live
        $hBack = Get-Sha256 $backup
        if ($hLive -eq $hBack) {
            Write-Ok ("restored from HEAD, byte-identical to r1 backup : " + $path)
        } else {
            # Byte hashes differ. The one legitimate reason is line endings: git writes the
            # checkout in its configured EOL while the pre-apply file on disk carried the
            # EOL the previous pack wrote. Prove that is the ONLY difference by comparing
            # the content with EOL and BOM normalised. Anything else is a real disagreement
            # and the pack stops rather than choosing.
            $nLive = Get-NormalisedTextSha256 $live
            $nBack = Get-NormalisedTextSha256 $backup
            if ($nLive -ne $nBack) {
                Stop-Refused ("HEAD content and the r1 backup DISAGREE beyond line endings for " + $path + " (HEAD " + $hLive.Substring(0,12) + " vs backup " + $hBack.Substring(0,12) + "; normalised " + $nLive.Substring(0,12) + " vs " + $nBack.Substring(0,12) + "). Not choosing silently.")
            }

            # Same content, different line endings. Git's own checkout bytes are kept: they
            # are clean by construction. Copying the backup's CRLF bytes over an LF checkout
            # would make git report the path as modified even though the content is equal.
            Write-Ok ("restored from HEAD : " + $path)
            Write-Ok ("   HEAD checkout " + $hLive.Substring(0,12) + " vs r1 backup " + $hBack.Substring(0,12) + ": IDENTICAL after EOL/BOM normalisation (" + $nLive.Substring(0,12) + "); difference is line endings only")
        }
    } else {
        Write-Warn ("restored from HEAD, no r1 backup to cross-check : " + $path)
    }
}

# ===========================================================================
# PHASE 4 - PROVE PRISTINE OWNED BASELINE
# ===========================================================================
Write-Step 'Phase 4 - pristine proof'

$OwnedAfter = @(Invoke-NativeLines -Exe 'git' -NativeArgs (@('status', '--porcelain', '--') + $Script:OwnedPaths) | Where-Object { $_ -ne '' })
if ($OwnedAfter.Count -ne 0) { Stop-Refused ("owned paths still dirty after recovery: " + ($OwnedAfter -join ' | ')) }
Write-Ok 'owned paths clean'

$HeadNow = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('rev-parse', 'HEAD'))[0].Trim()
if ($HeadNow -ne $HeadBefore) { Stop-Refused 'HEAD moved during recovery' }
Write-Ok 'HEAD unchanged'

$IndexNow = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('diff', '--cached', '--name-only') | Where-Object { $_ -ne '' })
if ($IndexNow.Count -ne 0) { Stop-Refused 'index not empty after recovery' }
Write-Ok 'index empty'

$ForeignNow = Get-ForeignFingerprint
if ($ForeignNow -ne $ForeignBefore) { Stop-Refused ("foreign fingerprint changed during recovery: " + $ForeignBefore + " -> " + $ForeignNow) }
Write-Ok ("foreign fingerprint preserved : " + $ForeignNow)

if (Test-Path -LiteralPath $VocabFile) { Stop-Refused 'vocabulary DATA file still exists on the pristine tree' }

foreach ($p in @($TestProj, $ScannerFile, $RuleTestFile, $GateTestFile, $SysTestFile, $BaselineFile, $InventoryFile)) {
    if (-not (Test-Path -LiteralPath $p)) { Stop-Refused ("required file missing: " + $p) }
}

$BaselineBefore     = Get-Content -LiteralPath $BaselineFile -Raw | ConvertFrom-Json
$FingerprintsBefore = @($BaselineBefore.grandfathered | ForEach-Object { $_.fingerprint })
$RetiredBefore      = @($BaselineBefore.retired)
$BaselineFileCountRecorded = $BaselineBefore.productGenericFileCount

Write-Ok ("committed ratchet: grandfathered = " + $FingerprintsBefore.Count + ", retired = " + $RetiredBefore.Count)
Write-Ok ("baseline-recorded productGenericFileCount = " + $BaselineFileCountRecorded + "  (metadata from " + $BaselineBefore.generatedAtUtc + ", NOT a live measurement)")
if ($FingerprintsBefore.Count -ne 49 -or $RetiredBefore.Count -ne 0) {
    Stop-Refused ("committed ratchet is not the expected 49/0 authority (found " + $FingerprintsBefore.Count + "/" + $RetiredBefore.Count + ")")
}

# ===========================================================================
# PHASE 5 - JOBLOG FOCUSED TEST ON THE PRISTINE TREE
# ===========================================================================
Write-Step 'Phase 5 - JobLog focused classification, pristine tree'

$jobPreLog = Join-Path $BackupDir 'joblog-pre.log'
$JobPreExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $jobPreLog -NativeArgs @(
    'test', $TestProj, '--filter', 'FullyQualifiedName~JobLogObservabilitySourceGuardTests', '--nologo')
$JobPreFails = @(Get-FailLines $jobPreLog)
Write-Ok ("JobLog pristine: exit " + $JobPreExit + ", failing tests " + $JobPreFails.Count)
foreach ($f in $JobPreFails) { Write-Host ("         " + $f) }

# ===========================================================================
# PHASE 6 - APPLY (manifest first, then backups, then mutation)
# ===========================================================================
Write-Step 'Phase 6 - owned manifest'
foreach ($rel in $Script:OwnedPaths) { Write-Host ("  " + $rel); Write-Host ("        " + $Script:OwnedReasons[$rel]) }

Write-Step 'Phase 6a - exact owned backups'
foreach ($rel in $Script:OwnedPaths) {
    $live = ConvertTo-Live $rel
    if (Test-Path -LiteralPath $live) {
        Copy-Item -LiteralPath $live -Destination (Join-Path $BackupDir (ConvertTo-BackupName $rel)) -Force
        Write-Ok ("backed up " + $rel)
    } else {
        Write-Ok ("to be created " + $rel)
    }
}

Write-Step 'Phase 6b - apply'
$Script:AppliedAnything = $true

# --- vocabulary DATA authority --------------------------------------------
$vocabJson = @'
{
  "task": "T-093",
  "release": "M2",
  "schema": 1,
  "note": "The authoritative plant-vocabulary list. These terms are DATA. They are not compiled into C# constants and not compiled into a regex alternative in any rule source. Adding or removing a term is an edit to this file and nothing else. Consumers select the terms that apply to them by declared consumer id, so there is one curated list in the repository and no consumer keeps a private copy.",
  "consumers": [
    {
      "id": "genericityGate",
      "description": "UA-08 in ScopeAwareGenericity: a registered plant term compiled into product-generic SEMANTICS or BEHAVIOUR - a compiled declaration, a hardcoded default or catalogue, a behavioural branch or discriminator, a semantic assignment. Not a textual echo."
    },
    {
      "id": "systemTemplateAuthority",
      "description": "SystemTemplateAuthorityTests: vocabulary that must never appear in the product system-template runtime authority."
    }
  ],
  "terms": [
    { "term": "ProductFamily", "consumers": ["genericityGate"], "note": "T-093 mandated seed. Backlog names DashboardWidgetQuerySafetyRegistry as the physical source; T-046 Pack 3A moved the catalogue, so the compiled declaration now lives in DashboardMetadataCodes.Dimensions and DashboardDimensionRegistry. Physical source corrected, intent unchanged." },
    { "term": "GradeOrRecipe", "consumers": ["genericityGate"], "note": "T-093 mandated seed." },
    { "term": "ShiftCode",     "consumers": ["genericityGate"], "note": "T-093 mandated seed." },
    { "term": "DefectType",    "consumers": ["genericityGate"], "note": "T-093 mandated seed." },
    { "term": "RiskClass",     "consumers": ["genericityGate"], "note": "T-093 mandated seed." },
    { "term": "CastingSpeed",  "consumers": ["systemTemplateAuthority"], "note": "Migrated verbatim from the compiled array in SystemTemplateAuthorityTests." },
    { "term": "coil",          "consumers": ["systemTemplateAuthority"], "note": "Migrated verbatim from the compiled array in SystemTemplateAuthorityTests." },
    { "term": "heat",          "consumers": ["systemTemplateAuthority"], "note": "Migrated verbatim from the compiled array in SystemTemplateAuthorityTests." },
    { "term": "caster",        "consumers": ["systemTemplateAuthority"], "note": "Migrated verbatim from the compiled array in SystemTemplateAuthorityTests." },
    { "term": "tundish",       "consumers": ["systemTemplateAuthority"], "note": "Migrated verbatim from the compiled array in SystemTemplateAuthorityTests." }
  ]
}
'@
Write-AsciiFile -Path $VocabFile -Content $vocabJson
Assert-Ascii -Path $VocabFile
$VocabData = Get-Content -LiteralPath $VocabFile -Raw | ConvertFrom-Json
$GateTerms = @($VocabData.terms | Where-Object { $_.consumers -contains 'genericityGate' } | ForEach-Object { $_.term })
$SysTerms  = @($VocabData.terms | Where-Object { $_.consumers -contains 'systemTemplateAuthority' } | ForEach-Object { $_.term })
$TermCount = @($VocabData.terms).Count
Write-Ok ("vocabulary DATA: " + $TermCount + " terms, genericityGate " + $GateTerms.Count + ", systemTemplate " + $SysTerms.Count)
if ($GateTerms.Count -eq 0 -or $SysTerms.Count -eq 0) { Invoke-Rollback 'a consumer projection is empty' }

# --- scanner ----------------------------------------------------------------
Invoke-AnchoredReplace -Path $ScannerFile -Label 'scanner: System.Text.Json using' -Old @'
using System.Text;
using System.Text.RegularExpressions;
'@ -New @'
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
'@

Invoke-AnchoredReplace -Path $ScannerFile -Label 'scanner: UA-08 in the doc contract' -Old @'
//   UA-07 behaviour inferred from customer vocabulary
//
'@ -New @'
//   UA-07 behaviour inferred from customer vocabulary
//   UA-08 registered plant vocabulary compiled into product-generic semantics or behaviour
//
// T-093. UA-08 is the only data-driven rule. Its terms live in
// plant_vocabulary_terms.json and are never written into this file. It is a semantic
// rule, not a word census: it fires on a declaration, a default, a catalogue entry, a
// branch or a semantic assignment, and stays silent on comments, log echoes, prose,
// examples, transport typing and generated derivative files.
//
'@

Invoke-AnchoredReplace -Path $ScannerFile -Label 'scanner: derivative scope value' -Old @'
public enum GenericityScope
{
    ProductGeneric,
    CustomerAsset,
    TestOrFixture,
    Documentation,
    Excluded
}
'@ -New @'
public enum GenericityScope
{
    ProductGeneric,
    CustomerAsset,
    TestOrFixture,
    Documentation,
    // T-093. A file mechanically generated from a semantic authority elsewhere in the
    // tree. Vocabulary it reproduces is that authority's debt, counted once, at the
    // authority. Bounded on purpose: only EF Designer and model-snapshot files qualify.
    // A handwritten migration is still product code and is still scanned.
    GeneratedDerivative,
    Excluded
}
'@

Invoke-AnchoredReplace -Path $ScannerFile -Label 'scanner: DATA authority self-exclusion' -Old @'
        "genericity_violation_baseline.json",
        "genericityviolationinventory.md"
    };
'@ -New @'
        "genericity_violation_baseline.json",
        "genericityviolationinventory.md",
        "plant_vocabulary_terms.json"
    };
'@

Invoke-AnchoredReplace -Path $ScannerFile -Label 'scanner: rule table becomes built' -Old @'
    public static IReadOnlyList<GenericityRule> Rules { get; } = new List<GenericityRule>
    {
'@ -New @'
    public static IReadOnlyList<GenericityRule> Rules { get; } = BuildRules();

    // T-093. The table is built rather than declared, because UA-08's alternatives come
    // from DATA. UA-01 to UA-07 are unchanged and still compiled: they encode constructs,
    // not vocabulary, and there is nothing about them for a customer to configure.
    private static IReadOnlyList<GenericityRule> BuildRules() => new List<GenericityRule>
    {
'@

Invoke-AnchoredReplace -Path $ScannerFile -Label 'scanner: UA-08 semantic rule builder' -Old @'
            "(indexof|contains|startswith|endswith)\\s*\\(\\s*\"(grade|coil|heat|slab|cast)\"")
    };

    public static string RepositoryRoot()
'@ -New @'
            "(indexof|contains|startswith|endswith)\\s*\\(\\s*\"(grade|coil|heat|slab|cast)\""),

        BuildVocabularyRule(
            PlantVocabularyAuthority.TermsFor(PlantVocabularyAuthority.GenericityGateConsumer))
    };

    // UA-08. Registered plant vocabulary compiled into product-generic semantics or
    // behaviour.
    //
    // The test the rule encodes: if this occurrence were removed or generalised, would
    // the generic product believe, permit, default to, branch on, expose or catalogue
    // something different? If yes, it is semantic authority and it fires. If the same
    // word merely echoes a semantic property that is declared elsewhere - in a log
    // template, a sentence of error prose, an example payload, a transport type, a file
    // generated from the model - it does not fire, because the declaration is the debt
    // and an echo is not a second assumption.
    //
    // Two lexical classes are used to tell those apart:
    //   IDENTIFIER forms are matched CASE-SENSITIVELY in the registered PascalCase, so a
    //   compiled symbol named for the term is caught while a camelCase parameter, local
    //   or property READ is not.
    //   LITERAL forms are matched only as the EXACT quoted term, never as a word inside
    //   a longer string, and only in positions where a literal drives behaviour or
    //   membership: comparison, case, default, fallback, assignment, element, argument.
    //
    // Constructs that fire:
    //   1. compiled constant or field named for the term        const string ShiftCode
    //   2. product-owned property or member declaration          public string ShiftCode {
    //   3. typed positional/record parameter in PascalCase       string? RiskClass,
    //   4. a line that is nothing but the term (enum/member)     RiskClass,
    //   5. literal assigned to a symbol named for the term       ShiftCode = "shift"
    //   6. behavioural comparison, case or discriminator         == "shiftCode"
    //   7. default, fallback, object value or assignment         ?? "shiftCode"   : "shiftCode",
    //   8. catalogue element or bare argument                    [ "shiftCode", ... ]   Resolve("shiftCode")
    //
    // A TypeScript type position (shiftCode: string) is transport typing of a governed
    // contract and does not fire; a TypeScript VALUE position (code: "shiftCode",
    // useState("shiftCode"), === "shiftCode") is product semantics and does.
    public static GenericityRule BuildVocabularyRule(IReadOnlyList<string> terms)
    {
        if (terms is null || terms.Count == 0)
        {
            throw new InvalidOperationException(
                "Genericity gate: UA-08 was built from an empty term list. A vocabulary rule with no " +
                "vocabulary passes forever and proves nothing. Check " +
                PlantVocabularyAuthority.RelativePath + ".");
        }

        var ident   = "(?:" + string.Join("|", terms.Select(Regex.Escape)) + ")";
        var literal = "\"" + ident + "\"";

        var alternatives = new[]
        {
            // 1. compiled constant or field named for the term
            "(?-i:\\b(?:const|readonly|static)\\s+string\\s+" + ident + "\\b)",

            // 2. product-owned property or member declaration
            "(?-i:\\b(?:public|internal|protected)\\s+(?:[\\w.<>\\[\\],?]+\\s+)+" + ident + "\\s*(?:\\{|=>))",

            // 3. typed positional or record parameter in PascalCase
            "(?-i:^\\s*[\\w.<>\\[\\]?]+\\s+" + ident + "\\s*[,)])",

            // 4. a line that is nothing but the term
            "(?-i:^\\s*" + ident + "\\s*,?\\s*$)",

            // 5. literal assigned to a symbol named for the term
            "(?-i:\\b" + ident + "\\s*=\\s*\")",

            // 6. behavioural comparison, case or discriminator
            "(?:==|===|!=|!==|\\bcase\\s+|\\.Equals\\(|\\bis\\s+)\\s*" + literal,
            literal + "\\s*(?:==|===|!=|!==)",

            // 7. default, fallback, object value or assignment
            "(?:\\?\\?|=|\\?|:)\\s*" + literal + "\\s*(?:;|,|\\)|\\}|$)",

            // 8. catalogue element or bare argument
            "[\\[{(,]\\s*" + literal + "\\s*[\\]}),]"
        };

        return new GenericityRule(
            "UA-08",
            "registered plant vocabulary compiled into product-generic semantics or behaviour",
            string.Join("|", alternatives));
    }

    public static string RepositoryRoot()
'@

Invoke-AnchoredReplace -Path $ScannerFile -Label 'scanner: derivative classification' -Old @'
        if (p.StartsWith("website/")) return GenericityScope.Excluded;
        if (!ProductExtensions.Contains(Path.GetExtension(p))) return GenericityScope.Excluded;

        return GenericityScope.ProductGeneric;
'@ -New @'
        if (p.StartsWith("website/")) return GenericityScope.Excluded;
        if (!ProductExtensions.Contains(Path.GetExtension(p))) return GenericityScope.Excluded;

        // T-093. EF Designer and model-snapshot files are generated from the entity
        // model; every term they carry is the model's debt, counted at the model. The
        // migration's own handwritten .cs stays ProductGeneric.
        if (p.Contains("/migrations/") && (name.EndsWith(".designer.cs") || name.EndsWith("modelsnapshot.cs")))
        {
            return GenericityScope.GeneratedDerivative;
        }

        // T-093. OpenAPI example material demonstrates a contract; it does not create one.
        if (p.Contains("/swagger/") && name.Contains("example"))
        {
            return GenericityScope.Documentation;
        }

        return GenericityScope.ProductGeneric;
'@

$authorityCs = @'

// ============================================================================
// THE PLANT-VOCABULARY DATA AUTHORITY.
//
// Backlog origin: T-093.
//
// One committed repository artifact. No table, no migration, no database mirror and
// no parity mechanism: the build must not need PostgreSQL to know what a plant term
// is. Consumers select terms by declared consumer id, so the genericity gate and the
// system-template gate read the same file and neither keeps a private list.
//
// A missing, unparsable or empty authority is a HARD FAILURE. A vocabulary gate that
// silently loses its vocabulary is green forever and guards nothing.
// ============================================================================

public sealed class PlantVocabularyTerm
{
    public PlantVocabularyTerm(string term, IReadOnlyList<string> consumers)
    {
        Term = term;
        Consumers = consumers;
    }

    public string Term { get; }
    public IReadOnlyList<string> Consumers { get; }
}

public static class PlantVocabularyAuthority
{
    public const string RelativePath =
        "Backend/tests/PlantProcess.Architecture.Tests/plant_vocabulary_terms.json";

    public const string GenericityGateConsumer = "genericityGate";
    public const string SystemTemplateConsumer = "systemTemplateAuthority";

    private static readonly Lazy<IReadOnlyList<PlantVocabularyTerm>> Cache = new(Load);

    public static IReadOnlyList<PlantVocabularyTerm> All => Cache.Value;

    public static IReadOnlyList<string> TermsFor(string consumerId)
    {
        return All
            .Where(t => t.Consumers.Contains(consumerId, StringComparer.Ordinal))
            .Select(t => t.Term)
            .ToArray();
    }

    public static string AbsolutePath()
    {
        return Path.Combine(
            ScopeAwareGenericity.RepositoryRoot(),
            RelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public static string DataSha256()
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(File.ReadAllBytes(AbsolutePath()));

        var sb = new StringBuilder(64);
        foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static IReadOnlyList<PlantVocabularyTerm> Load()
    {
        var path = AbsolutePath();

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "Plant vocabulary authority: " + RelativePath + " is missing. Deleting the term " +
                "list must fail the build, never silence the gate.");
        }

        var terms = new List<PlantVocabularyTerm>();

        using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
        {
            if (!doc.RootElement.TryGetProperty("terms", out var array))
            {
                throw new InvalidOperationException(
                    "Plant vocabulary authority: " + RelativePath + " declares no 'terms' array.");
            }

            foreach (var entry in array.EnumerateArray())
            {
                var term = entry.GetProperty("term").GetString();
                if (string.IsNullOrWhiteSpace(term)) continue;

                var consumers = new List<string>();
                if (entry.TryGetProperty("consumers", out var consumerArray))
                {
                    foreach (var c in consumerArray.EnumerateArray())
                    {
                        var id = c.GetString();
                        if (!string.IsNullOrWhiteSpace(id)) consumers.Add(id!);
                    }
                }

                terms.Add(new PlantVocabularyTerm(term!, consumers));
            }
        }

        if (terms.Count == 0)
        {
            throw new InvalidOperationException(
                "Plant vocabulary authority: " + RelativePath + " parsed to zero terms.");
        }

        return terms;
    }
}
'@
$scannerNow = [System.IO.File]::ReadAllText($ScannerFile)
Write-AsciiFile -Path $ScannerFile -Content ($scannerNow + $authorityCs) -Eol (Get-FileEol $scannerNow)
Assert-Ascii -Path $ScannerFile
Write-Ok 'applied: scanner: PlantVocabularyAuthority appended'

# --- rule falsification tests -----------------------------------------------
Invoke-AnchoredReplace -Path $RuleTestFile -Label 'rule tests: UA-08 sample in the theory' -Old @'
        data.Add("UA-07",
            "var isGrade = f.FeatureKey.IndexOf(\"grade\", StringComparison.OrdinalIgnoreCase) >= 0;",
            "var isStratum = f.FeatureKey.Equals(declared.StratumKey, StringComparison.Ordinal);");

        return data;
'@ -New @'
        data.Add("UA-07",
            "var isGrade = f.FeatureKey.IndexOf(\"grade\", StringComparison.OrdinalIgnoreCase) >= 0;",
            "var isStratum = f.FeatureKey.Equals(declared.StratumKey, StringComparison.Ordinal);");

        // T-093. UA-08 is data-driven; its sample uses a term the authority registers for
        // the gate consumer. The negative neighbour is the same intent through metadata.
        data.Add("UA-08",
            "public const string ShiftCode = \"shiftCode\";",
            "var dimension = registry.Resolve(request.DimensionCode);");

        return data;
'@

Invoke-AnchoredReplace -Path $RuleTestFile -Label 'rule tests: T-093 semantic falsification' -Old @'
    [Fact]
    public void The_product_generic_walk_is_not_vacuous()
'@ -New @'
    // ------------------------------------------------------------------------
    // T-093. UA-08 falsification. These assert the CONCEPTUAL classification the
    // CENTRAL ruling defined, through the real scanner, with the real scope classifier.
    // ------------------------------------------------------------------------

    private const string FrontendPath = "Frontend/PlantProcess.Web/src/state/sample.ts";

    private static bool Ua08(string path, string code) =>
        ScopeAwareGenericity.ScanText(path, code).Any(f => f.RuleId == "UA-08");

    public static TheoryData<string, string, string> Ua08Positives()
    {
        var data = new TheoryData<string, string, string>();

        data.Add("compiled constant declaration",     ProductPath,  "public const string ShiftCode = \"shiftCode\";");
        data.Add("product-owned property declaration", ProductPath, "public string? RiskClass { get; init; }");
        data.Add("record positional parameter",        ProductPath, "    string? ProductFamily,");
        data.Add("enum or member line",                ProductPath, "    DefectType,");
        data.Add("semantic default fallback",          ProductPath, "var dimension = request.Dimension ?? \"shiftCode\";");
        data.Add("behaviour branch on the literal",    ProductPath, "if (kind == \"riskClass\") { return Weighted(); }");
        data.Add("switch case on the literal",         ProductPath, "case \"defectType\": return BuildDefectSeries();");
        data.Add("compiled catalogue element",         ProductPath, "var supported = new[] { \"shiftCode\", \"defectType\" };");
        data.Add("bare argument selecting by term",    ProductPath, "var series = source.Resolve(\"gradeOrRecipe\");");
        data.Add("frontend catalogue entry",           FrontendPath, "  { code: \"shiftCode\", label: \"Shift\" },");
        data.Add("frontend default",                   FrontendPath, "const [dimension, setDimension] = useState(\"shiftCode\");");
        data.Add("frontend discriminator",             FrontendPath, "if (row.kind === \"riskClass\") { render(); }");

        return data;
    }

    public static TheoryData<string, string, string> Ua08Negatives()
    {
        var data = new TheoryData<string, string, string>();

        data.Add("property read only",                 ProductPath,  "var value = row.ShiftCode;");
        data.Add("camelCase parameter",                ProductPath,  "public Task RunAsync(string shiftCode, CancellationToken ct)");
        data.Add("comment mentioning the term",        ProductPath,  "// the ShiftCode dimension is declared by the customer\nvar x = 1;");
        data.Add("log interpolation echo",             ProductPath,  "_logger.LogInformation(\"Stored risk score. riskClass={RiskClass}\", score.RiskClass);");
        data.Add("human-readable error prose",         ProductPath,  "throw new ArgumentException(\"DefectType is required (pick a live quality event type).\");");
        data.Add("descriptive sentence literal",       ProductPath,  "new Descriptor(\"Product family, product group or manufacturing family.\");");
        data.Add("test fixture scope",                 "Backend/tests/PlantProcess.Sample.Tests/FixtureTests.cs", "public const string ShiftCode = \"shiftCode\";");
        data.Add("seed or customer asset scope",       "Backend/tools/seed_reference_plant.py", "SHIFT = \"ShiftCode\"");
        data.Add("swagger example scope",              "Backend/PlantProcess.Api/Swagger/SwaggerExamplesOperationFilter.cs", "[\"dimension\"] = \"shiftCode\",");
        data.Add("EF Designer derivative",             "Backend/PlantProcess.Infrastructure/Migrations/20260101_X.Designer.cs", "b.Property<string>(\"RiskClass\")");
        data.Add("EF model snapshot derivative",       "Backend/PlantProcess.Infrastructure/Migrations/PlantProcessDbContextModelSnapshot.cs", "b.Property<string>(\"RiskClass\")");
        data.Add("transport TypeScript typing",        FrontendPath, "export interface WidgetRow { shiftCode: string; riskClass?: string; }");
        data.Add("vocabulary DATA authority itself",   PlantVocabularyAuthority.RelativePath, "{ \"term\": \"ShiftCode\", \"consumers\": [\"genericityGate\"] }");

        return data;
    }

    [Theory]
    [MemberData(nameof(Ua08Positives))]
    public void Ua08_fires_on_semantic_authority(string label, string path, string code)
    {
        Assert.True(Ua08(path, code), "Genericity gate: UA-08 missed semantic authority: " + label);
    }

    [Theory]
    [MemberData(nameof(Ua08Negatives))]
    public void Ua08_stays_silent_on_echoes_and_out_of_scope(string label, string path, string code)
    {
        Assert.False(Ua08(path, code), "Genericity gate: UA-08 fired on an echo or out-of-scope text: " + label);
    }

    [Fact] // T-093. A handwritten migration is still product code; only the generated files are derivative.
    public void Derivative_scope_is_bounded_to_generated_migration_files()
    {
        Assert.Equal(GenericityScope.GeneratedDerivative, ScopeAwareGenericity.Classify("Backend/PlantProcess.Infrastructure/Migrations/20260101_X.Designer.cs"));
        Assert.Equal(GenericityScope.GeneratedDerivative, ScopeAwareGenericity.Classify("Backend/PlantProcess.Infrastructure/Migrations/PlantProcessDbContextModelSnapshot.cs"));
        Assert.Equal(GenericityScope.ProductGeneric,      ScopeAwareGenericity.Classify("Backend/PlantProcess.Infrastructure/Migrations/20260101_X.cs"));
        Assert.Equal(GenericityScope.ProductGeneric,      ScopeAwareGenericity.Classify("Backend/PlantProcess.Infrastructure/Persistence/PlantProcessDbContext.cs"));
        Assert.Equal(GenericityScope.Documentation,       ScopeAwareGenericity.Classify("Backend/PlantProcess.Api/Swagger/SwaggerExamplesOperationFilter.cs"));
        Assert.Equal(GenericityScope.ProductGeneric,      ScopeAwareGenericity.Classify("Backend/PlantProcess.Api/Swagger/SwaggerOperationFilter.cs"));
        Assert.Equal(GenericityScope.Excluded,            ScopeAwareGenericity.Classify(PlantVocabularyAuthority.RelativePath));
    }

    [Fact] // T-093. The vocabulary is DATA: the rule source compiles no gate term.
    public void The_scanner_source_contains_no_compiled_gate_term()
    {
        var scanner = ScopeAwareGenericity.StripComments(File.ReadAllText(Path.Combine(
            ScopeAwareGenericity.RepositoryRoot(),
            "Backend/tests/PlantProcess.Architecture.Tests/ScopeAwareGenericity.cs"
                .Replace('/', Path.DirectorySeparatorChar))));

        foreach (var term in PlantVocabularyAuthority.TermsFor(PlantVocabularyAuthority.GenericityGateConsumer))
        {
            Assert.DoesNotContain(term, scanner, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact] // T-093. Every registered gate term is actually enforced.
    public void The_vocabulary_rule_is_built_from_the_registered_terms()
    {
        var registered = PlantVocabularyAuthority.TermsFor(PlantVocabularyAuthority.GenericityGateConsumer);
        Assert.NotEmpty(registered);

        var rule = ScopeAwareGenericity.Rules.Single(r => r.Id == "UA-08");

        foreach (var term in registered)
        {
            Assert.Matches(rule.Pattern, "public const string " + term + " = \"value\";");
        }
    }

    [Fact] // T-093. Changing the vocabulary needs no C# change.
    public void A_rule_built_from_a_different_term_set_enforces_that_set_only()
    {
        var rule = ScopeAwareGenericity.BuildVocabularyRule(new[] { "WidgetFlavour" });

        Assert.Matches(rule.Pattern, "public const string WidgetFlavour = \"widgetFlavour\";");
        Assert.DoesNotMatch(rule.Pattern, "public const string ShiftCode = \"shiftCode\";");
    }

    [Fact] // T-093. A vocabulary rule with no vocabulary must refuse to exist.
    public void An_empty_term_set_is_refused()
    {
        Assert.Throws<InvalidOperationException>(
            () => ScopeAwareGenericity.BuildVocabularyRule(Array.Empty<string>()));
    }

    [Fact]
    public void The_product_generic_walk_is_not_vacuous()
'@
Assert-Ascii -Path $RuleTestFile

# --- generator: same generator, optional output directory -------------------
Invoke-AnchoredReplace -Path $GateTestFile -Label 'generator: output sink helper' -Old @'
    private static string Abs(string relative) =>
        Path.Combine(ScopeAwareGenericity.RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
'@ -New @'
    private static string Abs(string relative) =>
        Path.Combine(ScopeAwareGenericity.RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    // T-093. The ONE generator, with a second sink. When PPIQ_GENERICITY_OUTPUT_DIR is
    // set, the same scan writes its baseline and inventory under that directory instead
    // of the committed paths, so a candidate can be measured with the authoritative
    // scanner before anything in the repository moves. The scan, the fingerprints and
    // the inventory format are identical; only the destination differs.
    private static string OutputPath(string relative)
    {
        var dir = Environment.GetEnvironmentVariable("PPIQ_GENERICITY_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return Abs(relative);
        return Path.Combine(dir, Path.GetFileName(relative));
    }
'@

Invoke-AnchoredReplace -Path $GateTestFile -Label 'generator: baseline sink' -Old @'
        Directory.CreateDirectory(Path.GetDirectoryName(Abs(InventoryRelative))!);

        File.WriteAllText(
            Abs(BaselineRelative),
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
'@ -New @'
        var baselineOut  = OutputPath(BaselineRelative);
        var inventoryOut = OutputPath(InventoryRelative);

        Directory.CreateDirectory(Path.GetDirectoryName(baselineOut)!);
        Directory.CreateDirectory(Path.GetDirectoryName(inventoryOut)!);

        File.WriteAllText(
            baselineOut,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
'@

Invoke-AnchoredReplace -Path $GateTestFile -Label 'generator: inventory sink' -Old @'
        File.WriteAllText(Abs(InventoryRelative), md.ToString());
'@ -New @'
        File.WriteAllText(inventoryOut, md.ToString());
'@
Assert-Ascii -Path $GateTestFile

# --- system template authority ----------------------------------------------
Invoke-AnchoredReplace -Path $SysTestFile -Label 'system template: doc contract' -Old @'
// Every searched token is assembled from fragments and every read strips comments, so
// this file cannot satisfy its own rules and a comment cannot violate them.
'@ -New @'
// Every read strips comments, so a comment cannot violate the rules.
//
// T-093. The plant-vocabulary list is no longer curated here. It comes from the single
// DATA authority the genericity gate also reads, so the repository holds exactly one
// curated vocabulary list. Fragment assembly is no longer needed: no term is written in
// this file at all.
'@

Invoke-AnchoredReplace -Path $SysTestFile -Label 'system template: consume the DATA authority' -Old @'
    // Plant-specific vocabulary that must never appear in a product template.
    private static readonly string[] PlantVocabulary =
    {
        "Casting" + "Speed",
        "co" + "il",
        "he" + "at",
        "cas" + "ter",
        "tun" + "dish"
    };
'@ -New @'
    // Plant-specific vocabulary that must never appear in a product template.
    // T-093: DATA, from the one authority. The specialised invariant below is unchanged.
    private static IReadOnlyList<string> PlantVocabulary =>
        PlantVocabularyAuthority.TermsFor(PlantVocabularyAuthority.SystemTemplateConsumer);
'@

Invoke-AnchoredReplace -Path $SysTestFile -Label 'system template: non-vacuity guard' -Old @'
    public void Runtime_authority_contains_no_plant_specific_vocabulary()
    {
        var source = ReadRuntimeAuthority();
        var offenders = PlantVocabulary
'@ -New @'
    public void Runtime_authority_contains_no_plant_specific_vocabulary()
    {
        var source = ReadRuntimeAuthority();

        // T-093. An empty list would make this invariant vacuous rather than green.
        Assert.NotEmpty(PlantVocabulary);

        var offenders = PlantVocabulary
'@
Assert-Ascii -Path $SysTestFile

# ===========================================================================
# PHASE 7 - COMPILE, FALSIFICATION, SHARED DATA
# ===========================================================================
Write-Step 'Phase 7 - compile'
$buildLog = Join-Path $BackupDir 'build.log'
$buildExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $buildLog -NativeArgs @('build', $TestProj, '--nologo')
if ($buildExit -ne 0) { Invoke-Rollback ("compile failed; see " + $buildLog) }

$OwnedWarnings = @(Get-Content -LiteralPath $buildLog | Where-Object {
    ($_ -match 'warning') -and (
        $_ -match 'ScopeAwareGenericity\.cs' -or $_ -match 'ScopeAwareGenericityRuleTests\.cs' -or
        $_ -match 'GenericityBaselineGateTests\.cs' -or $_ -match 'SystemTemplateAuthorityTests\.cs')
} | Sort-Object -Unique)
if ($OwnedWarnings.Count -ne 0) {
    foreach ($w in $OwnedWarnings) { Write-Bad ("   " + $w) }
    Invoke-Rollback ("compile introduced " + $OwnedWarnings.Count + " analyzer warning(s) in owned files")
}
Write-Ok 'compiles; zero analyzer warnings in owned files'

Write-Step 'Phase 7a - UA-08 positive and negative falsification'
$falsifyLog = Join-Path $BackupDir 'falsification.log'
$falsifyExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $falsifyLog -NativeArgs @(
    'test', $TestProj, '--filter', 'FullyQualifiedName~ScopeAwareGenericityRuleTests', '--nologo', '--no-build')
if ($falsifyExit -ne 0) { foreach ($f in (Get-FailLines $falsifyLog)) { Write-Bad ("   " + $f) }; Invoke-Rollback ("falsification failed; see " + $falsifyLog) }
$PassedMatches = @(Get-Content -LiteralPath $falsifyLog | Select-String -Pattern 'Passed:\s+(\d+)')
$FalsifyPassed = 'n/a'
if ($PassedMatches.Count -gt 0) { $FalsifyPassed = $PassedMatches[-1].Matches[0].Groups[1].Value }
Write-Ok ("falsification GREEN (" + $FalsifyPassed + " tests)")

Write-Step 'Phase 7b - system-template invariant on the shared DATA'
$sysLog = Join-Path $BackupDir 'system-template.log'
$sysExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $sysLog -NativeArgs @(
    'test', $TestProj, '--filter', 'Gate=SystemTemplateAuthority', '--nologo', '--no-build')
if ($sysExit -ne 0) { Invoke-Rollback ("system-template authority gate failed; see " + $sysLog) }
Write-Ok 'system-template invariant GREEN'

# ===========================================================================
# PHASE 8 - AUTHORITATIVE CANDIDATE MEASUREMENT (same generator, temp sink)
# ===========================================================================
Write-Step 'Phase 8 - candidate measurement with the authoritative scanner'

$env:PPIQ_GENERICITY_WRITE_BASELINE = '1'
$env:PPIQ_GENERICITY_OUTPUT_DIR = $CandidateDir
$candLog = Join-Path $BackupDir 'candidate.log'
$candExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $candLog -NativeArgs @(
    'test', $TestProj, '--filter', 'FullyQualifiedName~GenericityBaselineGateTests.Generate_baseline_and_inventory_when_explicitly_asked', '--nologo', '--no-build')
Remove-Item Env:\PPIQ_GENERICITY_OUTPUT_DIR -ErrorAction SilentlyContinue
Remove-Item Env:\PPIQ_GENERICITY_WRITE_BASELINE -ErrorAction SilentlyContinue
if ($candExit -ne 0) { Invoke-Rollback ("candidate generation failed; see " + $candLog) }

$CandBaselinePath = Join-Path $CandidateDir 'genericity_violation_baseline.json'
if (-not (Test-Path -LiteralPath $CandBaselinePath)) { Invoke-Rollback 'candidate baseline was not written to the temporary sink' }

$OwnedMid = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('status', '--porcelain', '--', 'Backend/tests/PlantProcess.Architecture.Tests/genericity_violation_baseline.json', 'docs/quality/GenericityViolationInventory.md') | Where-Object { $_ -ne '' })
if ($OwnedMid.Count -ne 0) { Invoke-Rollback 'candidate measurement mutated the committed baseline or inventory; the sink is not isolated' }
Write-Ok 'candidate written outside the repository; committed ratchet untouched'

$Cand = Get-Content -LiteralPath $CandBaselinePath -Raw | ConvertFrom-Json
$CandEntries = @($Cand.grandfathered)
$LiveFileCount = $Cand.productGenericFileCount
$BeforeSet = @{}; foreach ($f in $FingerprintsBefore) { $BeforeSet[$f] = $true }
$CandAdded = @($CandEntries | Where-Object { -not $BeforeSet.ContainsKey($_.fingerprint) })
$CandSet = @{}; foreach ($e in $CandEntries) { $CandSet[$e.fingerprint] = $true }
$CandVanished = @($FingerprintsBefore | Where-Object { -not $CandSet.ContainsKey($_) })

Write-Ok ("live-scanned productGenericFileCount     = " + $LiveFileCount)
Write-Ok ("baseline-recorded productGenericFileCount = " + $BaselineFileCountRecorded + "  (stale metadata, for contrast only)")
Write-Ok ("candidate total   = " + $CandEntries.Count)
Write-Ok ("existing 49 still discoverable = " + (49 - $CandVanished.Count) + " / 49")
Write-Ok ("candidate UA-08 N = " + $CandAdded.Count + " across " + @($CandAdded | Group-Object path).Count + " files")

if ($CandVanished.Count -ne 0) { Invoke-Rollback ("candidate: " + $CandVanished.Count + " existing fingerprint(s) no longer discoverable") }
$CandNonUa08 = @($CandAdded | Where-Object { $_.rule -ne 'UA-08' })
if ($CandNonUa08.Count -ne 0) { Invoke-Rollback ("candidate: " + $CandNonUa08.Count + " added fingerprint(s) are not UA-08") }
if ($CandAdded.Count -eq 0) { Invoke-Rollback 'candidate: UA-08 found nothing on a tree known to carry compiled dimension vocabulary; broken rule, not clean tree' }

Write-Host ''
Write-Host 'UA-08 candidate by file:'
foreach ($g in ($CandAdded | Group-Object path | Sort-Object Count -Descending)) { Write-Host ("  {0,3}  {1}" -f $g.Count, $g.Name) }

# Reporting shape only - a label on the construct the authoritative scanner captured,
# never a second classification.
function Get-ConstructShape {
    param([string] $c)
    if ($c -match '^(const|readonly|static)\s')                 { return 'declaration: compiled constant' }
    if ($c -match '^(public|internal|protected)\s')              { return 'declaration: property or member' }
    if ($c -match '^[a-z0-9_.<>\[\]?]+\s+[a-z]+\s*[,)]$')         { return 'declaration: positional parameter' }
    if ($c -match '^[a-z]+\s*,?$')                               { return 'declaration: enum or member line' }
    if ($c -match '^[a-z]+\s*=\s*"$')                            { return 'assignment: literal to term symbol' }
    if ($c -match '^(==|===|!=|!==|case\s|\.equals\(|is\s)')     { return 'behaviour: comparison or case' }
    if ($c -match '^"[a-z]+"\s*(==|===|!=|!==)')                 { return 'behaviour: comparison' }
    if ($c -match '^(\?\?|=|\?|:)')                              { return 'default, fallback or value' }
    if ($c -match '^[\[{(,]')                                    { return 'catalogue element or argument' }
    return 'other'
}
Write-Host ''
Write-Host 'UA-08 candidate by construct shape:'
$ShapeGroups = @($CandAdded | ForEach-Object { Get-ConstructShape $_.construct } | Group-Object | Sort-Object Count -Descending)
foreach ($g in $ShapeGroups) { Write-Host ("  {0,3}  {1}" -f $g.Count, $g.Name) }
Write-Host ''
Write-Host 'representative samples:'
foreach ($e in ($CandAdded | Sort-Object path | Select-Object -First 12)) { Write-Host ("  [" + $e.path.Split('/')[-1] + "] " + $e.construct) }

# ===========================================================================
# PHASE 9 - GENERATE THE REAL BASELINE AND INVENTORY (same generator)
# ===========================================================================
Write-Step 'Phase 9 - generate the committed baseline and inventory'
$env:PPIQ_GENERICITY_WRITE_BASELINE = '1'
$genLog = Join-Path $BackupDir 'regenerate.log'
$genExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $genLog -NativeArgs @(
    'test', $TestProj, '--filter', 'FullyQualifiedName~GenericityBaselineGateTests.Generate_baseline_and_inventory_when_explicitly_asked', '--nologo', '--no-build')
Remove-Item Env:\PPIQ_GENERICITY_WRITE_BASELINE -ErrorAction SilentlyContinue
if ($genExit -ne 0) { Invoke-Rollback ("baseline generation failed; see " + $genLog) }
Write-Ok 'generated'

# ===========================================================================
# PHASE 10 - MECHANICAL RATCHET GUARDS
# ===========================================================================
Write-Step 'Phase 10 - ratchet guards'

$BaselineAfter = Get-Content -LiteralPath $BaselineFile -Raw | ConvertFrom-Json
$AfterEntries  = @($BaselineAfter.grandfathered)
$AfterSet = @{}; foreach ($g in $AfterEntries) { $AfterSet[$g.fingerprint] = $g }

$Vanished = @($FingerprintsBefore | Where-Object { -not $AfterSet.ContainsKey($_) })
if ($Vanished.Count -ne 0) { Invoke-Rollback ("guard 1: " + $Vanished.Count + " pre-existing fingerprint(s) vanished; T-093 removes no debt") }
Write-Ok 'guard 1: all 49 pre-existing fingerprints preserved'

$Added = @($AfterEntries | Where-Object { -not $BeforeSet.ContainsKey($_.fingerprint) })
$NonUa08 = @($Added | Where-Object { $_.rule -ne 'UA-08' })
if ($NonUa08.Count -ne 0) { Invoke-Rollback ("guard 2: " + $NonUa08.Count + " added fingerprint(s) are not UA-08") }
Write-Ok ("guard 2: all " + $Added.Count + " added fingerprints are UA-08")

if ($AfterEntries.Count -ne ($FingerprintsBefore.Count + $Added.Count)) { Invoke-Rollback 'guard 3: arithmetic does not close' }
Write-Ok ("guard 3: " + $FingerprintsBefore.Count + " + " + $Added.Count + " = " + $AfterEntries.Count)

if (@($BaselineAfter.retired).Count -ne $RetiredBefore.Count) { Invoke-Rollback 'guard 4: retired set changed' }
Write-Ok 'guard 4: retired unchanged'

$CandAddedSet = @{}; foreach ($e in $CandAdded) { $CandAddedSet[$e.fingerprint] = $true }
$GenNotInCand = @($Added | Where-Object { -not $CandAddedSet.ContainsKey($_.fingerprint) })
if ($Added.Count -ne $CandAdded.Count -or $GenNotInCand.Count -ne 0) {
    Invoke-Rollback ("guard 5: candidate measurement (" + $CandAdded.Count + ") and generated baseline (" + $Added.Count + ") disagree on the same source")
}
Write-Ok ("guard 5: candidate == generated, " + $Added.Count + " UA-08, identical fingerprint set")

if ($BaselineAfter.productGenericFileCount -ne $LiveFileCount) { Invoke-Rollback 'guard 6: live file count differs between candidate and generated scans' }
Write-Ok ("guard 6: live productGenericFileCount stable at " + $LiveFileCount)

Write-Step 'Phase 10b - full genericity gate against the regenerated ratchet'
$gateLog = Join-Path $BackupDir 'post-apply-gate.log'
$gateExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $gateLog -NativeArgs @(
    'test', $TestProj, '--filter', 'BacklogTask=T-206', '--nologo', '--no-build')
if ($gateExit -ne 0) { foreach ($f in (Get-FailLines $gateLog)) { Write-Bad ("   " + $f) }; Invoke-Rollback ("post-apply genericity gate failed; see " + $gateLog) }
Write-Ok 'zero unknown fingerprints; baseline and inventory agree'

# ===========================================================================
# PHASE 11 - JOBLOG FOCUSED TEST AFTER APPLY
# ===========================================================================
Write-Step 'Phase 11 - JobLog focused classification, after apply'
$jobPostLog = Join-Path $BackupDir 'joblog-post.log'
$JobPostExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $jobPostLog -NativeArgs @(
    'test', $TestProj, '--filter', 'FullyQualifiedName~JobLogObservabilitySourceGuardTests', '--nologo', '--no-build')
$JobPostFails = @(Get-FailLines $jobPostLog)

$JobClassification = ''
if ($JobPreExit -eq 0 -and $JobPostExit -eq 0) {
    $JobClassification = 'GREEN before and after'
} elseif ($JobPreExit -ne 0 -and $JobPostExit -ne 0 -and (($JobPreFails -join '|') -eq ($JobPostFails -join '|'))) {
    $JobClassification = 'INHERITED / PRE-EXISTING - the same ' + $JobPreFails.Count + ' test(s) fail identically before and after T-093; not caused here, not fixed here'
    foreach ($n in $JobPreFails) { Write-Host ('         same before and after: ' + $n) }
} elseif ($JobPreExit -eq 0 -and $JobPostExit -ne 0) {
    foreach ($n in $JobPostFails) { Write-Bad ('   appeared after T-093: ' + $n) }
    Invoke-Rollback 'JobLog passes on the pristine tree and fails after T-093: a T-093 regression'
} else {
    foreach ($n in ($JobPreFails  | Where-Object { $JobPostFails -notcontains $_ })) { Write-Bad ('   failed BEFORE only: ' + $n) }
    foreach ($n in ($JobPostFails | Where-Object { $JobPreFails  -notcontains $_ })) { Write-Bad ('   failed AFTER only : ' + $n) }
    Invoke-Rollback ('JobLog failure set is not equivalent before and after (pre exit ' + $JobPreExit + '/' + $JobPreFails.Count + ' test(s), post exit ' + $JobPostExit + '/' + $JobPostFails.Count + ' test(s)); not waived')
}
Write-Ok ("JobLog: " + $JobClassification)

# ===========================================================================
# PHASE 12 - ADJACENT ARCHITECTURE REGRESSION, ONCE
# ===========================================================================
Write-Step 'Phase 12 - architecture project regression, once'
$regLog = Join-Path $BackupDir 'architecture-regression.log'
$regExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $regLog -NativeArgs @('test', $TestProj, '--nologo', '--no-build')

$RegStatus = 'GREEN'
if ($regExit -ne 0) {
    $RegFails = @(Get-FailLines $regLog)
    if ($RegFails.Count -eq 0) { Invoke-Rollback ("regression exit " + $regExit + " with no [FAIL] line; see " + $regLog) }

    $Unexpected = @($RegFails | Where-Object { $JobPostFails -notcontains $_ })
    foreach ($f in $RegFails) {
        if ($JobPostFails -contains $f) { Write-Warn ('   INHERITED  ' + $f) } else { Write-Bad ('   NEW        ' + $f) }
    }
    if ($Unexpected.Count -ne 0) { Invoke-Rollback ("regression: " + $Unexpected.Count + " failure(s) beyond the classified inherited JobLog failure") }
    $RegStatus = 'GREEN except INHERITED JobLog failure, unchanged before/after T-093'
}
Write-Ok ("regression: " + $RegStatus)

# ===========================================================================
# PHASE 13 - EXACT STAGING
# ===========================================================================
Write-Step 'Phase 13 - exact staging'
foreach ($rel in $Script:OwnedPaths) {
    if (-not (Test-Path -LiteralPath (ConvertTo-Live $rel))) { Invoke-Rollback ("owned path missing: " + $rel) }
    $null = Invoke-NativeLines -Exe 'git' -NativeArgs @('add', '--', $rel)
    if ($Script:LastNativeExit -ne 0) { Invoke-Rollback ("git add failed for " + $rel) }
}
$Staged = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('diff', '--cached', '--name-only') | Where-Object { $_ -ne '' })
foreach ($s in $Staged) { Write-Host ("         " + $s) }
if ($Staged.Count -ne $Script:OwnedPaths.Count) { Invoke-Rollback ("staged " + $Staged.Count + " paths, expected " + $Script:OwnedPaths.Count) }
foreach ($s in $Staged) { if ($Script:OwnedPaths -notcontains ($s -replace '\\', '/')) { Invoke-Rollback ("unowned path in index: " + $s) } }
Write-Ok 'index == owned manifest'

$CheckOut = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('diff', '--cached', '--check'))
foreach ($c in $CheckOut) { Write-Host ("         " + $c) }
if ($Script:LastNativeExit -ne 0) { Invoke-Rollback 'git diff --cached --check reported whitespace damage' }
Write-Ok 'git diff --cached --check GREEN'

$ForeignPreCommit = Get-ForeignFingerprint
if ($ForeignPreCommit -ne $ForeignBefore) { Invoke-Rollback ("foreign fingerprint changed before commit: " + $ForeignBefore + " -> " + $ForeignPreCommit) }
Write-Ok ("foreign fingerprint preserved : " + $ForeignPreCommit)

# ===========================================================================
# PHASE 14 - COMMIT
# ===========================================================================
Write-Step 'Phase 14 - commit'
$Subject = 'T-093 govern plant vocabulary as data-driven genericity debt'
$CommitOut = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('commit', '-q', '-m', $Subject))
foreach ($c in $CommitOut) { Write-Host ("         " + $c) }
if ($Script:LastNativeExit -ne 0) {
    Write-Bad 'git commit failed. Owned paths are LEFT IN PLACE and staged for inspection. T-093 is NOT closed.'
    exit 4
}
$HeadAfter = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('rev-parse', 'HEAD'))[0].Trim()

# ===========================================================================
# PHASE 15 - POST-COMMIT CLOSURE PROOF
# ===========================================================================
Write-Step 'Phase 15 - post-commit proof'
$IndexAfter = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('diff', '--cached', '--name-only') | Where-Object { $_ -ne '' })
$Committed  = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('show', '--pretty=format:', '--name-only', 'HEAD') | Where-Object { $_ -ne '' })
$ForeignAfter = Get-ForeignFingerprint
$VocabSha = Get-Sha256 $VocabFile

$r = New-Object System.Text.StringBuilder
[void]$r.AppendLine('T-093 CLOSED GREEN')
[void]$r.AppendLine('')
[void]$r.AppendLine('parent SHA          : ' + $HeadBefore)
[void]$r.AppendLine('commit SHA          : ' + $HeadAfter)
[void]$r.AppendLine('commit subject      : ' + $Subject)
[void]$r.AppendLine('branch              : ' + $Branch)
[void]$r.AppendLine('')
[void]$r.AppendLine('committed paths (' + $Committed.Count + ')')
foreach ($c in $Committed) { [void]$r.AppendLine('  ' + $c) }
[void]$r.AppendLine('')
[void]$r.AppendLine('vocabulary DATA hash        : ' + $VocabSha)
[void]$r.AppendLine('vocabulary term count       : ' + $TermCount)
[void]$r.AppendLine('genericity consumer count   : ' + $GateTerms.Count)
[void]$r.AppendLine('system-template consumer    : ' + $SysTerms.Count)
[void]$r.AppendLine('')
[void]$r.AppendLine('baseline-recorded productGenericFileCount (old metadata) : ' + $BaselineFileCountRecorded)
[void]$r.AppendLine('live-scanned productGenericFileCount                     : ' + $LiveFileCount)
[void]$r.AppendLine('new ratchet productGenericFileCount                      : ' + $BaselineAfter.productGenericFileCount)
[void]$r.AppendLine('')
[void]$r.AppendLine('existing grandfathered      = ' + $FingerprintsBefore.Count)
[void]$r.AppendLine('new UA-08                   = ' + $Added.Count + ' across ' + @($Added | Group-Object path).Count + ' files')
[void]$r.AppendLine('final grandfathered         = ' + $AfterEntries.Count)
[void]$r.AppendLine('retired                     = ' + @($BaselineAfter.retired).Count + ' (unchanged)')
[void]$r.AppendLine('unknown                     = 0')
[void]$r.AppendLine('candidate == generated      = YES (' + $CandAdded.Count + ' == ' + $Added.Count + ', same fingerprint set)')
[void]$r.AppendLine('')
[void]$r.AppendLine('UA-08 by construct shape')
foreach ($g in $ShapeGroups) { [void]$r.AppendLine(('  {0,3}  {1}' -f $g.Count, $g.Name)) }
[void]$r.AppendLine('')
[void]$r.AppendLine('UA-08 positive falsification = GREEN')
[void]$r.AppendLine('UA-08 negative falsification = GREEN')
[void]$r.AppendLine('SystemTemplate shared DATA   = GREEN')
[void]$r.AppendLine('focused genericity gate      = GREEN')
[void]$r.AppendLine('owned analyzer warnings      = 0')
[void]$r.AppendLine('')
[void]$r.AppendLine('JobLog pre-result   : exit ' + $JobPreExit + ', failing ' + $JobPreFails.Count)
[void]$r.AppendLine('JobLog post-result  : exit ' + $JobPostExit + ', failing ' + $JobPostFails.Count)
[void]$r.AppendLine('JobLog classification : ' + $JobClassification)
[void]$r.AppendLine('')
[void]$r.AppendLine('adjacent architecture regression : ' + $RegStatus)
[void]$r.AppendLine('')
[void]$r.AppendLine('index after commit  = ' + $(if ($IndexAfter.Count -eq 0) { 'EMPTY' } else { 'NOT EMPTY (' + $IndexAfter.Count + ')' }))
[void]$r.AppendLine('foreign before      = ' + $ForeignBefore)
[void]$r.AppendLine('foreign after       = ' + $ForeignAfter)
[void]$r.AppendLine('foreign preserved   = ' + $(if ($ForeignAfter -eq $ForeignBefore) { 'YES' } else { 'NO' }))
[void]$r.AppendLine('')
[void]$r.AppendLine('handover to T-094   : ' + $AfterEntries.Count + ' grandfathered fingerprints, ' + $Added.Count + ' of them UA-08; inventory in docs/quality/GenericityViolationInventory.md')
[void]$r.AppendLine('evidence            : ' + $BackupDir)
[void]$r.AppendLine('candidate artifacts : ' + $CandidateDir)

$reportPath = Join-Path $EvidenceDir ('T-093_closure_' + $Stamp + '.txt')
Write-AsciiFile -Path $reportPath -Content $r.ToString()

Write-Step 'RESULT'
Write-Host $r.ToString()
Write-Host ('report: ' + $reportPath)

if ($IndexAfter.Count -ne 0 -or $ForeignAfter -ne $ForeignBefore) { exit 4 }
exit 0
