# Retire-CompiledPlantDimensions.ps1
#
# T-094  Wave A, part 2a: the five plant dimensions stop being compiled product
#        identity. Owner Worker 1 (Lane A). Release M2-P1.
#
# Part 1 built the declared-dimension execution path and proved it. This pack cuts
# the compiled one. After it:
#
#   - DashboardMetadataCodes.Dimensions holds STRUCTURAL grammar only: identity,
#     provenance and calendar. productFamily, gradeOrRecipe, shiftCode, defectType
#     and riskClass are gone from product code;
#   - the aggregate engine has no branch, member map entry or label for them;
#   - DashboardDimensionRegistry describes nine dimensions, not fourteen;
#   - the metadata catalogue no longer advertises them, so the product never offers
#     a dimension it would then refuse;
#   - three system-template widgets that assumed a plant vocabulary are gone from
#     the generic install;
#   - the process-step population now serves any dimension DECLARED against the
#     step entity, chosen by its binding and never by its name.
#
# CONSEQUENCE, STATED PLAINLY. On a database where nobody has declared these
# concepts they are simply absent, which is the intended generic install. On a
# database that already shows them - ppiq_presentation with its flat-steel data -
# they return only once they are authored as published definitions. That authoring
# is presentation DATA and ships separately; it is not part of the generic bootstrap.
#
# MODE. Default is Validate: apply, run every gate, print the result, then restore
# the owned paths and leave the repository exactly as found. Pass -Mode Commit to
# keep and commit the change. Part 2a is backend-internal and does not alter the
# request contract, so it may commit on its own; the filter-contract change that
# follows may not, and lands with Lane B.
#
# Exit codes: 0 gates green (committed only in Commit mode). 2 refused before
#             mutation. 3 a gate failed, owned paths restored. 4 commit failed.

[CmdletBinding()]
param(
    [string] $RepoRoot    = 'C:\Workspace\PlantProcess-IQ',
    [string] $BackupRoot  = 'C:\Workspace\_ppiq_backups',
    [string] $EvidenceDir = 'C:\Workspace\_ppiq_evidence',
    [ValidateSet('Validate', 'Commit')]
    [string] $Mode        = 'Validate'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$Stamp     = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $BackupRoot ("t094a2_" + $Stamp)
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

function Get-NonAsciiCount {
    param([string] $Path)
    $count = 0
    foreach ($b in [System.IO.File]::ReadAllBytes($Path)) { if ($b -gt 127) { $count++ } }
    return $count
}

# DIFFERENTIAL, for the same reason the warning gate is. A file this pack edits is
# not a file this pack owns end to end: DashboardDefinitionService.cs already
# carries an em dash in a comment that predates T-094 and is outside its scope.
# Asserting the WHOLE file is ASCII refuses someone else's byte; asserting that the
# count did not RISE refuses only bytes this pack wrote.
function Assert-NoNewNonAscii {
    param([string] $Rel)

    $live = ConvertTo-Live $Rel
    $backup = Join-Path $BackupDir (ConvertTo-BackupName $Rel)

    $after = Get-NonAsciiCount $live
    $before = 0
    if (Test-Path -LiteralPath $backup) { $before = Get-NonAsciiCount $backup }

    if ($after -gt $before) {
        Invoke-Rollback ("this pack wrote " + ($after - $before) + " non-ASCII byte(s) into " + $Rel)
    }
    if ($before -gt 0) {
        Write-Warn ("   inherited non-ASCII bytes: " + $before + " in " + $Rel + " (unchanged)")
    }
}

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
    param([string] $Path, [string] $Old, [string] $New, [string] $Label, [switch] $RemoveLine)

    # RAW BYTES IN, RAW BYTES OUT. The previous version normalised the whole file to
    # LF, replaced, then rewrote it with one detected line ending. On a file whose
    # line endings are MIXED that rewrites every line, and git reports the entire
    # file as changed - hundreds of lines of "trailing whitespace" for a four-line
    # edit. The anchor is matched against the file exactly as it is on disk, and
    # every byte outside the anchor is left untouched.
    if (-not (Test-Path -LiteralPath $Path)) { Invoke-Rollback ("anchored replace: missing file " + $Path) }

    $raw = [System.IO.File]::ReadAllText($Path)

    $count = 0
    $idx = 0
    while ($true) {
        $idx = $raw.IndexOf($Old, $idx, [System.StringComparison]::Ordinal)
        if ($idx -lt 0) { break }
        $count++
        $idx += $Old.Length
    }

    if ($count -ne 1) {
        # Say WHY, so a line-ending mismatch is never mistaken for a moved anchor.
        $rawLf = $raw -replace "`r`n", "`n"
        $oldLf = $Old -replace "`r`n", "`n"
        $lfCount = 0
        $j = 0
        while ($true) {
            $j = $rawLf.IndexOf($oldLf, $j, [System.StringComparison]::Ordinal)
            if ($j -lt 0) { break }
            $lfCount++
            $j += $oldLf.Length
        }

        if ($lfCount -eq 1 -and $count -eq 0) {
            Invoke-Rollback ("anchored replace '" + $Label + "': the anchor matches " + $Path +
                " only after line-ending normalisation, so this file's line endings differ from the pack's. " +
                "Rewriting it would rewrite every line. Use single-line anchors for this file.")
        }

        Invoke-Rollback ("anchored replace '" + $Label + "': anchor matched " + $count + " times in " + $Path)
    }

    $start = $raw.IndexOf($Old, [System.StringComparison]::Ordinal)
    $end = $start + $Old.Length

    if ($RemoveLine) {
        # DELETE THE LINE, NOT THE TEXT ON IT. Replacing a line's text with nothing
        # leaves whatever followed it - and a source line can carry trailing spaces
        # after the last token. The remnant is then a whitespace-only line, which is
        # exactly what "git diff --cached --check" refuses. Consume the trailing
        # blanks and the line terminator so no remnant can exist.
        while ($end -lt $raw.Length -and ($raw[$end] -eq ' ' -or $raw[$end] -eq "`t")) { $end++ }
        if ($end -lt $raw.Length -and $raw[$end] -eq "`r") { $end++ }
        if ($end -lt $raw.Length -and $raw[$end] -eq "`n") { $end++ }
    }

    [System.IO.File]::WriteAllText($Path, $raw.Substring(0, $start) + $New + $raw.Substring($end), $Utf8NoBom)
    Write-Ok ("applied: " + $Label)
}

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


function Get-OwnedWarningCounts {
    param([string[]] $LogPaths)

    # A warning is identified by CODE + OWNED FILE, never by line number: inserting
    # lines moves every warning below the insertion point, and a moved warning is the
    # same warning. Counting per key still catches a genuinely NEW warning of a code
    # that already occurs in that file.
    $counts = @{}
    $seen = @{}

    foreach ($logPath in $LogPaths) {
        if (-not (Test-Path -LiteralPath $logPath)) { continue }

        foreach ($line in (Get-Content -LiteralPath $logPath)) {
            if ($line -notmatch 'warning\s+([A-Za-z]+[0-9]+)') { continue }
            $code = $Matches[1]

            foreach ($rel in $Script:OwnedPaths) {
                $needle = $rel -replace '/', '\'
                if ($line.IndexOf($needle, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) { continue }

                # MSBuild prints each warning twice (build output and summary).
                if ($seen.ContainsKey($line)) { break }
                $seen[$line] = $true

                $key = $code + ' in ' + $rel
                if ($counts.ContainsKey($key)) { $counts[$key] = $counts[$key] + 1 } else { $counts[$key] = 1 }
                break
            }
        }
    }

    return $counts
}


# ---------------------------------------------------------------------------
# OWNED MANIFEST
# ---------------------------------------------------------------------------
$Script:OwnedPaths = @(
    'Backend/PlantProcess.Application/Dashboarding/Contracts/DashboardMetadataDtos.cs',
    'Backend/PlantProcess.Application/Dashboarding/Services/Dashboards/DashboardDefinitionService.cs',
    'Backend/PlantProcess.Application/Dashboarding/Services/Metadata/DashboardMetadataService.cs',
    'Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardAggregateExecutor.cs',
    'Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardWidgetQueryService.cs',
    'Backend/PlantProcess.Application/Dashboarding/Services/Queries/WidgetResultSources.cs',
    'Backend/PlantProcess.Application/Dashboarding/Services/Widgets/DashboardDimensionRegistry.cs',
    'Backend/tests/PlantProcess.Architecture.Tests/DimensionRegistrySingleAuthorityTests.cs'
)

$Script:OwnedReasons = @{
    'Backend/PlantProcess.Application/Dashboarding/Contracts/DashboardMetadataDtos.cs' = 'The five plant dimension constants are retired; structural grammar remains.'
    'Backend/PlantProcess.Application/Dashboarding/Services/Dashboards/DashboardDefinitionService.cs' = 'Three system-template widgets that assumed a plant vocabulary are removed.'
    'Backend/PlantProcess.Application/Dashboarding/Services/Metadata/DashboardMetadataService.cs' = 'Filter catalogue and purpose sets stop advertising the five.'
    'Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardAggregateExecutor.cs' = 'Member map, key selector and label chain lose the plant vocabulary.'
    'Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardWidgetQueryService.cs' = 'Label switch loses the vocabulary; population choice becomes binding-driven.'
    'Backend/PlantProcess.Application/Dashboarding/Services/Queries/WidgetResultSources.cs' = 'Two grouping switches lose the vocabulary.'
    'Backend/PlantProcess.Application/Dashboarding/Services/Widgets/DashboardDimensionRegistry.cs' = 'Five descriptors retired; nine structural dimensions remain.'
    'Backend/tests/PlantProcess.Architecture.Tests/DimensionRegistrySingleAuthorityTests.cs' = 'Registry count and categorical sample follow the retirement.'
}

$DtosPath = 'Backend/PlantProcess.Application/Dashboarding/Contracts/DashboardMetadataDtos.cs'
$ExecPath = 'Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardAggregateExecutor.cs'
$SvcPath = 'Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardWidgetQueryService.cs'
$SourcesPath = 'Backend/PlantProcess.Application/Dashboarding/Services/Queries/WidgetResultSources.cs'
$MetaPath = 'Backend/PlantProcess.Application/Dashboarding/Services/Metadata/DashboardMetadataService.cs'
$RegPath = 'Backend/PlantProcess.Application/Dashboarding/Services/Widgets/DashboardDimensionRegistry.cs'
$DefPath = 'Backend/PlantProcess.Application/Dashboarding/Services/Dashboards/DashboardDefinitionService.cs'
$RegTestsPath = 'Backend/tests/PlantProcess.Architecture.Tests/DimensionRegistrySingleAuthorityTests.cs'

$TestDir  = 'Backend\tests\PlantProcess.Architecture.Tests'
$TestProj = Join-Path $RepoRoot ($TestDir + '\PlantProcess.Architecture.Tests.csproj')
$ApiProj  = Join-Path $RepoRoot 'Backend\PlantProcess.Api\PlantProcess.Api.csproj'

# ===========================================================================
# PHASE 0 - REPOSITORY STATE
# ===========================================================================
Write-Step ('Phase 0 - repository state (mode: ' + $Mode + ')')
if (-not (Test-Path -LiteralPath $RepoRoot)) { Stop-Refused ("repository root not found: " + $RepoRoot) }
Set-Location -LiteralPath $RepoRoot
if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) { Stop-Refused 'dotnet is not on PATH' }
if ($null -eq (Get-Command git -ErrorAction SilentlyContinue)) { Stop-Refused 'git is not on PATH' }

$HeadBefore = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('rev-parse', 'HEAD'))[0].Trim()
$Branch     = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('rev-parse', '--abbrev-ref', 'HEAD'))[0].Trim()
Write-Ok ("HEAD   : " + $HeadBefore)
Write-Ok ("branch : " + $Branch)

$IndexBefore = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('diff', '--cached', '--name-only') | Where-Object { $_ -ne '' })
if ($IndexBefore.Count -ne 0) { Stop-Refused ("index is not empty (" + $IndexBefore.Count + " staged paths)") }
Write-Ok 'index is empty'

$ForeignBefore = Get-ForeignFingerprint
Write-Ok ("foreign fingerprint : " + $ForeignBefore)
foreach ($d in @($EvidenceDir, $BackupDir)) { if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null } }

Write-Step 'Phase 1 - owned paths'
$OwnedStatus = @(Invoke-NativeLines -Exe 'git' -NativeArgs (@('status', '--porcelain', '--') + $Script:OwnedPaths) | Where-Object { $_ -ne '' })
foreach ($s in $OwnedStatus) { Write-Host ("         " + $s) }
if ($OwnedStatus.Count -ne 0) { Stop-Refused ("owned paths are dirty before apply (" + $OwnedStatus.Count + "); inspect and clear them first") }
foreach ($rel in $Script:OwnedPaths) { if (-not (Test-Path -LiteralPath (ConvertTo-Live $rel))) { Stop-Refused ("missing owned path: " + $rel) } }
Write-Ok 'owned paths are clean and present'

# The binding contract from part 1 must already be committed; this pack removes the
# compiled path and would otherwise leave nothing able to serve a declared dimension.
foreach ($required in @(
    'Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DeclaredDimensionProjection.cs',
    'Backend/PlantProcess.Infrastructure/Dashboarding/Dimensions/DeclaredDimensionCatalog.cs')) {
    if (-not (Test-Path -LiteralPath (ConvertTo-Live $required))) {
        Stop-Refused ("part 1 is not present: " + $required + ". Removing the compiled dimensions without the declared binding would leave no path at all.")
    }
}
Write-Ok 'part 1 binding contract present'

# ===========================================================================
# PHASE 2 - PRISTINE BASELINE
# ===========================================================================
Write-Step 'Phase 2 - warning baseline, pristine tree'

# --no-incremental IS THE POINT. MSBuild only re-emits a project's warnings when it
# actually recompiles it, so a baseline taken from an up-to-date build reports zero
# warnings and then every inherited one looks introduced. Both sides of this
# comparison are forced full builds, or the comparison is between a measurement and
# a skipped step.
$basePreLog = Join-Path $BackupDir 'baseline-build.log'
$basePreExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $basePreLog -NativeArgs @('build', $ApiProj, '--no-incremental', '--nologo')
if ($basePreExit -ne 0) { Stop-Refused 'the pristine tree does not build; nothing here is measurable' }

$basePreTestLog = Join-Path $BackupDir 'baseline-build-tests.log'
$basePreTestExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $basePreTestLog -NativeArgs @('build', $TestProj, '--no-incremental', '--nologo')
if ($basePreTestExit -ne 0) { Stop-Refused 'the pristine test project does not build; nothing here is measurable' }

$WarnBefore = Get-OwnedWarningCounts -LogPaths @($basePreLog, $basePreTestLog)
Write-Ok ("pristine warnings in owned files : " + $WarnBefore.Count + " distinct")
foreach ($k in ($WarnBefore.Keys | Sort-Object)) { Write-Host ("         inherited x" + $WarnBefore[$k] + "  " + $k) }

# A baseline of zero across eight edited files is far more likely to be a skipped
# build than a clean tree, and a zero baseline turns every inherited warning into a
# false regression. Refuse to measure against it.
$BaselineWarningLines = @(Get-Content -LiteralPath $basePreLog, $basePreTestLog | Where-Object { $_ -match 'warning\s+[A-Za-z]+[0-9]+' })
if ($BaselineWarningLines.Count -eq 0) {
    Stop-Refused 'the pristine build emitted no warnings at all, so it did not really compile. The baseline would be empty and the differential gate meaningless.'
}
Write-Ok ("baseline build emitted " + $BaselineWarningLines.Count + " warning line(s) overall; the baseline is a real measurement")

Write-Step 'Phase 2b - JobLog focused classification, pristine tree'
$jobPreLog = Join-Path $BackupDir 'joblog-pre.log'
$JobPreExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $jobPreLog -NativeArgs @(
    'test', $TestProj, '--filter', 'FullyQualifiedName~JobLogObservabilitySourceGuardTests', '--nologo', '--no-build')
$JobPreFails = @(Get-FailLines $jobPreLog)
Write-Ok ("JobLog pristine: exit " + $JobPreExit + ", failing tests " + $JobPreFails.Count)

# ===========================================================================
# PHASE 3 - MANIFEST, BACKUPS, APPLY
# ===========================================================================
Write-Step 'Phase 3 - owned manifest'
foreach ($rel in $Script:OwnedPaths) { Write-Host ("  " + $rel); Write-Host ("        " + $Script:OwnedReasons[$rel]) }

Write-Step 'Phase 3a - exact owned backups'
foreach ($rel in $Script:OwnedPaths) {
    Copy-Item -LiteralPath (ConvertTo-Live $rel) -Destination (Join-Path $BackupDir (ConvertTo-BackupName $rel)) -Force
    Write-Ok ("backed up " + $rel)
}

Write-Step 'Phase 3b - apply'
$Script:AppliedAnything = $true

Invoke-AnchoredReplace -Path (ConvertTo-Live $DtosPath) -Label 'codes: retire the five plant dimension constants' -Old @'
        public const string MaterialUnitType = "materialUnitType";
        public const string ProductFamily = "productFamily";
        public const string GradeOrRecipe = "gradeOrRecipe";
        public const string ShiftCode = "shiftCode";
        public const string DefectType = "defectType";
        public const string ParameterCode = "parameterCode";
        public const string Day = "day";
        public const string Week = "week";
        public const string Month = "month";
        public const string RiskClass = "riskClass";
    }
'@ -New @'
        public const string MaterialUnitType = "materialUnitType";
        public const string ParameterCode = "parameterCode";
        public const string Day = "day";
        public const string Week = "week";
        public const string Month = "month";

        // T-094. What remains here is STRUCTURAL grammar: identity, provenance and
        // calendar. Concepts that describe a particular plant's product, quality,
        // risk or crew vocabulary are not product identity and are not compiled.
        // They arrive as published declarations and execute through the declared
        // binding contract, so a customer adds, renames or removes one without a
        // build. On an install where none is declared, none exists.
    }
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $ExecPath) -Label 'executor: structural member map only' -Old @'
            "materialunittype" => "MaterialUnitType",
            "productfamily" => "ProductFamily",
            "gradeorrecipe" => "GradeOrRecipe",
            "shiftcode" => "ShiftCode",
            "defecttype" => "DefectType",
            "parametercode" => "ParameterCode",
            "riskclass" => "RiskClass",
            "$declared" => DeclaredDimensionProjection.SlotMember,
'@ -New @'
            "materialunittype" => "MaterialUnitType",
            "parametercode" => "ParameterCode",
            "$declared" => DeclaredDimensionProjection.SlotMember,
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $ExecPath) -Label 'executor: key selector loses the plant vocabulary' -Old @'
        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.ProductFamily))
            return f => new DashboardGroupKey { Text = f.ProductFamily };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.GradeOrRecipe))
            return f => new DashboardGroupKey { Text = f.GradeOrRecipe };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.ShiftCode))
            return f => new DashboardGroupKey { Text = f.ShiftCode };

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.DefectType))
            return f => new DashboardGroupKey { Text = f.DefectType };

'@ -New ''

Invoke-AnchoredReplace -Path (ConvertTo-Live $ExecPath) -Label 'executor: risk class branch retired' -Old @'
        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.RiskClass))
            return f => new DashboardGroupKey { Text = f.RiskClass };

        if (DeclaredDimensionProjection.IsSlot(dimensionCode))
'@ -New @'
        if (DeclaredDimensionProjection.IsSlot(dimensionCode))
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $ExecPath) -Label 'executor: label chain loses the plant vocabulary' -Old @'
        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.ProductFamily))
            return FromText(key.Text, "No product family");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.GradeOrRecipe))
            return FromText(key.Text, "No grade / recipe");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.ShiftCode))
            return FromText(key.Text, "No shift");

        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.DefectType))
            return FromText(key.Text, "No defect");

'@ -New ''

Invoke-AnchoredReplace -Path (ConvertTo-Live $ExecPath) -Label 'executor: risk class label retired' -Old @'
        if (IsCode(dimensionCode, DashboardMetadataCodes.Dimensions.RiskClass))
            return FromText(key.Text, "No risk class");

        if (DeclaredDimensionProjection.IsSlot(dimensionCode))
'@ -New @'
        if (DeclaredDimensionProjection.IsSlot(dimensionCode))
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $SvcPath) -Label 'service: label switch loses the plant vocabulary' -Old @'
            DashboardMetadataCodes.Dimensions.ProductFamily =>
                BuildDimension(fact.ProductFamily, fact.ProductFamily, "No product family"),

            DashboardMetadataCodes.Dimensions.GradeOrRecipe =>
                BuildDimension(fact.GradeOrRecipe, fact.GradeOrRecipe, "No grade / recipe"),

            DashboardMetadataCodes.Dimensions.ShiftCode =>
                BuildDimension(fact.ShiftCode, fact.ShiftCode, "No shift"),

            DashboardMetadataCodes.Dimensions.DefectType =>
                BuildDimension(fact.DefectType, fact.DefectType, "No defect"),

            DashboardMetadataCodes.Dimensions.ParameterCode =>
'@ -New @'
            DashboardMetadataCodes.Dimensions.ParameterCode =>
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $SvcPath) -Label 'service: risk class label arm retired' -Old @'
            DashboardMetadataCodes.Dimensions.RiskClass =>
                BuildDimension(fact.RiskClass, fact.RiskClass, "No risk class"),

            DashboardMetadataCodes.Dimensions.Day =>
'@ -New @'
            DashboardMetadataCodes.Dimensions.Day =>
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $SvcPath) -Label 'service: using for the process-step entity' -Old @'
using PlantProcess.Domain.Entities.Materials;
'@ -New @'
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.Process;
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $SvcPath) -Label 'service: population choice is structural or declared' -Old @'
        if (IsDimension(resolved, DashboardMetadataCodes.Dimensions.Equipment) ||
            IsDimension(resolved, DashboardMetadataCodes.Dimensions.ShiftCode) ||
            IsDimension(resolved, DashboardMetadataCodes.Dimensions.Area))
        {
'@ -New @'
        // T-094. The process-step population answers for anything carried by a
        // step: its equipment, its area, and any dimension the customer declared
        // against the step entity itself. Which population serves a declared
        // dimension is decided by its binding, never by its name.
        var declaredOnStep = declared is not null && declared.SourceEntityType == typeof(ProcessStepExecution);

        if (declaredOnStep ||
            (declared is null &&
             (IsDimension(resolved, DashboardMetadataCodes.Dimensions.Equipment) ||
              IsDimension(resolved, DashboardMetadataCodes.Dimensions.Area))))
        {
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $SvcPath) -Label 'service: step projection binds a declared dimension' -Old @'
            var stepFacts =
                from step in _dbContext.ProcessStepExecutions.AsNoTracking()
                join equipment in _dbContext.Equipment.AsNoTracking()
                    on step.EquipmentId equals equipment.Id
                where
                    !step.IsDeleted &&
                    materialIds.Contains(step.MaterialUnitId)
                select new WidgetFact
                {
                    MaterialUnitId = step.MaterialUnitId,
                    AreaId = equipment.AreaId,
                    EquipmentId = step.EquipmentId,
                    SourceSystem = step.SourceSystem,
                    ShiftCode = step.CrewCode,
                    EventTimeUtc = step.StartedAtUtc,
                    Value = 1m
                };

            return await DashboardAggregateExecutor.ExecuteAsync(
                stepFacts,
'@ -New @'
            Expression<Func<ProcessStepExecution, WidgetFact>> stepProjection = step => new WidgetFact
            {
                MaterialUnitId = step.MaterialUnitId,
                EquipmentId = step.EquipmentId,
                SourceSystem = step.SourceSystem,
                EventTimeUtc = step.StartedAtUtc,
                Value = 1m
            };

            if (declared is not null)
            {
                stepProjection = DeclaredDimensionProjection.WithDeclaredDimension(stepProjection, declared);
            }

            var stepFacts = _dbContext.ProcessStepExecutions
                .AsNoTracking()
                .Where(step => !step.IsDeleted && materialIds.Contains(step.MaterialUnitId))
                .Select(stepProjection);

            return await DashboardAggregateExecutor.ExecuteAsync(
                stepFacts,
'@

# WidgetResultSources.cs is edited ONE LINE AT A TIME on purpose. A multi-line
# anchor also asserts the blank lines between the lines it spans, and blank-line
# layout is exactly what a reformat changes without changing meaning. A single
# line that is unique in the file is the smallest anchor that still cannot match
# the wrong place.
Invoke-AnchoredReplace -Path (ConvertTo-Live $SourcesPath) -Label 'sources: grouping switch drops gradeOrRecipe' -Old @'
                DashboardMetadataCodes.Dimensions.GradeOrRecipe => x.GradeOrRecipe,
'@ -New '' -RemoveLine

Invoke-AnchoredReplace -Path (ConvertTo-Live $SourcesPath) -Label 'sources: grouping switch drops productFamily' -Old @'
                DashboardMetadataCodes.Dimensions.ProductFamily => x.ProductFamily,
'@ -New '' -RemoveLine

Invoke-AnchoredReplace -Path (ConvertTo-Live $SourcesPath) -Label 'sources: defect grouping drops gradeOrRecipe' -Old @'
                    grouping == DashboardMetadataCodes.Dimensions.GradeOrRecipe ? material.GradeOrRecipe :
'@ -New '' -RemoveLine

Invoke-AnchoredReplace -Path (ConvertTo-Live $SourcesPath) -Label 'sources: defect grouping drops productFamily' -Old @'
                    grouping == DashboardMetadataCodes.Dimensions.ProductFamily ? material.ProductFamily :
'@ -New '' -RemoveLine

Invoke-AnchoredReplace -Path (ConvertTo-Live $MetaPath) -Label 'metadata: filter catalogue loses the plant vocabulary' -Old @'
            Filter("defectType", "Defect Type", "Quality", "string", "single", false, "defects", "Limit analysis to one defect type."),
            Filter("riskClass", "Risk Class", "Risk", "string", "single", false, "riskClasses", "Limit analysis to one risk class."),
            Filter("shiftCode", "Shift / Crew", "Operations", "string", "single", false, "shifts", "Limit analysis to one shift or crew."),

'@ -New '' -RemoveLine

Invoke-AnchoredReplace -Path (ConvertTo-Live $MetaPath) -Label 'metadata: purpose day set' -Old @'
                new[] { "day", "defectType", "equipment", "shiftCode" },
'@ -New @'
                new[] { "day", "equipment" },
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $MetaPath) -Label 'metadata: purpose day set' -Old @'
                new[] { "day", "equipment", "shiftCode", "materialUnitType" },
'@ -New @'
                new[] { "day", "equipment", "materialUnitType" },
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $MetaPath) -Label 'metadata: purpose day set' -Old @'
                new[] { "day", "equipment", "shiftCode", "sourceSystem" },
'@ -New @'
                new[] { "day", "equipment", "sourceSystem" },
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $MetaPath) -Label 'metadata: purpose riskClass set' -Old @'
                new[] { "riskClass", "day", "equipment", "productFamily" },
'@ -New @'
                new[] { "day", "equipment" },
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $MetaPath) -Label 'metadata: purpose equipment set' -Old @'
                new[] { "equipment", "parameterCode", "defectType", "sourceSystem" },
'@ -New @'
                new[] { "equipment", "parameterCode", "sourceSystem" },
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $RegPath) -Label 'registry: retire four plant descriptors' -Old @'
        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.ProductFamily,
            "Product Family",
            "Material",
            "string",
            false,
            new[] { "bar", "pie", "donut", "table" },
            "Product family, product group or manufacturing family."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.GradeOrRecipe,
            "Grade / Recipe",
            "Material",
            "string",
            false,
            new[] { "bar", "pie", "donut", "table" },
            "Grade, recipe, product code or process recipe."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.ShiftCode,
            "Shift / Crew",
            "Operations",
            "string",
            false,
            new[] { "bar", "pie", "donut", "heatmap", "table" },
            "Operational shift or crew code."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.DefectType,
            "Defect Type",
            "Quality",
            "string",
            false,
            new[] { "bar", "pie", "donut", "heatmap", "table" },
            "Standardized defect or quality event type."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.ParameterCode,
'@ -New @'
        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.ParameterCode,
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $RegPath) -Label 'registry: retire the risk descriptor' -Old @'
            "Calendar month bucket."),

        new DashboardDimensionDescriptor(
            DashboardMetadataCodes.Dimensions.RiskClass,
            "Risk Class",
            "Risk",
            "string",
            false,
            new[] { "bar", "pie", "donut", "table" },
            "Low, medium, high or critical risk classification.")
    };
'@ -New @'
            "Calendar month bucket.")

        // T-094. This registry describes the dimensions the product itself owns:
        // identity, provenance and calendar. A customer's own dimensions are
        // published declarations, not entries here, and reach execution through
        // the declared binding contract.
    };
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $DefPath) -Label 'templates: DEFECT_BREAKDOWN assumed a plant vocabulary' -Old @'
            TemplateWidget("DEFECT_BREAKDOWN", "Defect Breakdown", "bar", DashboardMetadataCodes.Dimensions.DefectType, DashboardMetadataCodes.Measures.DefectCount, 1),

'@ -New '' -RemoveLine

Invoke-AnchoredReplace -Path (ConvertTo-Live $DefPath) -Label 'templates: RISK_BY_CLASS assumed a plant vocabulary' -Old @'
            TemplateWidget("RISK_BY_CLASS", "Risk by Class", "bar", DashboardMetadataCodes.Dimensions.RiskClass, DashboardMetadataCodes.Measures.RiskScore, 0),

'@ -New '' -RemoveLine

Invoke-AnchoredReplace -Path (ConvertTo-Live $DefPath) -Label 'templates: DQ_BY_RISK_CLASS assumed a plant vocabulary' -Old @'
            TemplateWidget("DQ_BY_RISK_CLASS", "Issues by Risk Class", "bar", DashboardMetadataCodes.Dimensions.RiskClass, DashboardMetadataCodes.Measures.DataQualityIssueCount, 2),

'@ -New '' -RemoveLine

Invoke-AnchoredReplace -Path (ConvertTo-Live $RegTestsPath) -Label 'tests: the registry holds structural grammar only' -Old @'
        Assert.Equal(14, DashboardDimensionRegistry.All.Count);
'@ -New @'
        // T-094. Nine STRUCTURAL dimensions remain compiled. The plant-vocabulary
        // ones are published declarations now, so this count is the product's own
        // grammar and must not grow when a customer declares something.
        Assert.Equal(9, DashboardDimensionRegistry.All.Count);
'@

Invoke-AnchoredReplace -Path (ConvertTo-Live $RegTestsPath) -Label 'tests: categorical sample is a structural dimension' -Old @'
        Assert.Equal(AxisRole.Categorical, DashboardDimensionRegistry.AxisRoleOf(DashboardMetadataCodes.Dimensions.RiskClass));
'@ -New @'
        Assert.Equal(AxisRole.Categorical, DashboardDimensionRegistry.AxisRoleOf(DashboardMetadataCodes.Dimensions.MaterialUnitType));
'@


foreach ($rel in $Script:OwnedPaths) { Assert-NoNewNonAscii -Rel $rel }

# MECHANICAL PROOF, not a promise: no compiled reference to a retired constant may
# survive anywhere in the backend, including files this pack does not own.
Write-Step 'Phase 3c - retirement sweep'
$Retired = @('ProductFamily', 'GradeOrRecipe', 'ShiftCode', 'DefectType', 'RiskClass')
$Survivors = @()
foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'Backend') -Recurse -Filter *.cs -File |
                   Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($term in $Retired) {
        if ($text.Contains('DashboardMetadataCodes.Dimensions.' + $term)) {
            $Survivors += ($file.FullName.Substring($RepoRoot.Length + 1) + '  ->  Dimensions.' + $term)
        }
    }
}
if ($Survivors.Count -ne 0) {
    foreach ($s in ($Survivors | Sort-Object -Unique)) { Write-Bad ('   ' + $s) }
    Invoke-Rollback ("retirement sweep found " + $Survivors.Count + " surviving compiled reference(s)")
}
Write-Ok 'zero surviving compiled references to the retired dimensions'

# ===========================================================================
# PHASE 4 - COMPILE
# ===========================================================================
Write-Step 'Phase 4 - compile'
$buildLog = Join-Path $BackupDir 'build.log'
$buildExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $buildLog -NativeArgs @('build', $ApiProj, '--no-incremental', '--nologo')
if ($buildExit -ne 0) {
    foreach ($l in (Get-Content -LiteralPath $buildLog | Where-Object { $_ -match 'error CS' } | Sort-Object -Unique | Select-Object -First 40)) { Write-Bad ('   ' + $l) }
    Invoke-Rollback ("compile failed; full log " + $buildLog)
}
$buildTestLog = Join-Path $BackupDir 'build-tests.log'
$buildTestExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $buildTestLog -NativeArgs @('build', $TestProj, '--no-incremental', '--nologo')
if ($buildTestExit -ne 0) {
    foreach ($l in (Get-Content -LiteralPath $buildTestLog | Where-Object { $_ -match 'error CS' } | Sort-Object -Unique | Select-Object -First 40)) { Write-Bad ('   ' + $l) }
    Invoke-Rollback ("test project compile failed; full log " + $buildTestLog)
}
$WarnAfter = Get-OwnedWarningCounts -LogPaths @($buildLog, $buildTestLog)
$Introduced = @()
foreach ($k in $WarnAfter.Keys) {
    $before = 0
    if ($WarnBefore.ContainsKey($k)) { $before = $WarnBefore[$k] }
    if ($WarnAfter[$k] -gt $before) { $Introduced += ($k + "  (before " + $before + ", after " + $WarnAfter[$k] + ")") }
}
$Inherited = @($WarnAfter.Keys | Where-Object { $WarnBefore.ContainsKey($_) } | Sort-Object)
foreach ($k in $Inherited) { Write-Warn ("   INHERITED  " + $k) }
if ($Introduced.Count -ne 0) {
    foreach ($w in ($Introduced | Sort-Object)) { Write-Bad ("   INTRODUCED " + $w) }
    Invoke-Rollback ("compile introduced " + $Introduced.Count + " NEW warning(s) in owned files")
}
Write-Ok ("compiles; 0 new warnings in owned files, " + $Inherited.Count + " inherited and unchanged")

# ===========================================================================
# PHASE 5 - GATES
# ===========================================================================
Write-Step 'Phase 5 - declared binding still green'
$bindLog = Join-Path $BackupDir 'binding.log'
$bindExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $bindLog -NativeArgs @('test', $TestProj, '--filter', 'FullyQualifiedName~DeclaredDimensionBindingTests', '--nologo', '--no-build')
if ($bindExit -ne 0) { foreach ($f in (Get-FailLines $bindLog)) { Write-Bad ('   ' + $f) }; Invoke-Rollback ("binding falsification failed; see " + $bindLog) }
Write-Ok 'binding contract GREEN'

Write-Step 'Phase 5b - genericity gate green, and the UA-08 count must FALL'

# CORRECTION. An earlier version of this pack demanded the gate go RED after a
# removal. That was wrong: a ratchet blocks ADDITIONS. A grandfathered fingerprint
# that disappears from source is an improvement, and the gate is right to allow it.
# Redness proves nothing here. What must be proven is that the UA-08 population
# actually shrank, so this measures it with the same authoritative generator.
$genLog = Join-Path $BackupDir 'genericity.log'
$genExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $genLog -NativeArgs @('test', $TestProj, '--filter', 'BacklogTask=T-206', '--nologo', '--no-build')
if ($genExit -ne 0) {
    foreach ($f in (Get-FailLines $genLog)) { Write-Bad ('   ' + $f) }
    Invoke-Rollback ("genericity gate is red: this removal must not introduce an unknown fingerprint; see " + $genLog)
}
Write-Ok 'genericity gate GREEN: zero unknown fingerprints'

$CandidateDir = Join-Path $EvidenceDir ('t094a2_candidate_' + $Stamp)
if (-not (Test-Path -LiteralPath $CandidateDir)) { New-Item -ItemType Directory -Path $CandidateDir -Force | Out-Null }

$env:PPIQ_GENERICITY_WRITE_BASELINE = '1'
$env:PPIQ_GENERICITY_OUTPUT_DIR = $CandidateDir
$measureLog = Join-Path $BackupDir 'ua08-measure.log'
$measureExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $measureLog -NativeArgs @(
    'test', $TestProj, '--filter', 'FullyQualifiedName~GenericityBaselineGateTests.Generate_baseline_and_inventory_when_explicitly_asked', '--nologo', '--no-build')
Remove-Item Env:\PPIQ_GENERICITY_OUTPUT_DIR -ErrorAction SilentlyContinue
Remove-Item Env:\PPIQ_GENERICITY_WRITE_BASELINE -ErrorAction SilentlyContinue
if ($measureExit -ne 0) { Invoke-Rollback ("UA-08 re-measurement failed; see " + $measureLog) }

$CandidatePath = Join-Path $CandidateDir 'genericity_violation_baseline.json'
if (-not (Test-Path -LiteralPath $CandidatePath)) { Invoke-Rollback 'the re-measurement wrote nothing to the temporary sink' }

# The committed ratchet must not have moved: the measurement goes to a sink outside
# the repository, exactly as T-093 built it.
$RatchetDirty = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('status', '--porcelain', '--', 'Backend/tests/PlantProcess.Architecture.Tests/genericity_violation_baseline.json', 'docs/quality/GenericityViolationInventory.md') | Where-Object { $_ -ne '' })
if ($RatchetDirty.Count -ne 0) { Invoke-Rollback 'the re-measurement mutated the committed ratchet; the sink is not isolated' }

$CommittedBaseline = Get-Content -LiteralPath (ConvertTo-Live 'Backend/tests/PlantProcess.Architecture.Tests/genericity_violation_baseline.json') -Raw | ConvertFrom-Json
$CandidateBaseline = Get-Content -LiteralPath $CandidatePath -Raw | ConvertFrom-Json

$Ua08Before = @($CommittedBaseline.grandfathered | Where-Object { $_.rule -eq 'UA-08' }).Count
$Ua08After  = @($CandidateBaseline.grandfathered | Where-Object { $_.rule -eq 'UA-08' }).Count
$Removed    = $Ua08Before - $Ua08After

Write-Ok ("UA-08 in the committed ratchet : " + $Ua08Before)
Write-Ok ("UA-08 in current source        : " + $Ua08After)
Write-Ok ("retired by this pack           : " + $Removed)

if ($Ua08After -ge $Ua08Before) {
    Invoke-Rollback ("the UA-08 population did not shrink (" + $Ua08Before + " -> " + $Ua08After + "). The removal did not reach the scanner.")
}

$OtherBefore = @($CommittedBaseline.grandfathered | Where-Object { $_.rule -ne 'UA-08' }).Count
$OtherAfter  = @($CandidateBaseline.grandfathered | Where-Object { $_.rule -ne 'UA-08' }).Count
if ($OtherAfter -ne $OtherBefore) {
    Invoke-Rollback ("the 49 inherited historical fingerprints changed (" + $OtherBefore + " -> " + $OtherAfter + "); T-094 does not touch them")
}
Write-Ok ("inherited historical fingerprints unchanged at " + $OtherBefore)

Write-Step 'Phase 5c - dashboard, widget and dimension suites'
$dashLog = Join-Path $BackupDir 'dashboard-targeted.log'
$dashExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $dashLog -NativeArgs @('test', $TestProj, '--filter', 'FullyQualifiedName~Dashboard|FullyQualifiedName~Widget|FullyQualifiedName~Dimension', '--nologo', '--no-build')
if ($dashExit -ne 0) { foreach ($f in (Get-FailLines $dashLog)) { Write-Bad ('   ' + $f) }; Invoke-Rollback ("adjacent dashboard/widget/dimension tests failed; see " + $dashLog) }
Write-Ok 'adjacent dashboard, widget and dimension tests GREEN'

Write-Step 'Phase 6 - JobLog after apply'
$jobPostLog = Join-Path $BackupDir 'joblog-post.log'
$JobPostExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $jobPostLog -NativeArgs @('test', $TestProj, '--filter', 'FullyQualifiedName~JobLogObservabilitySourceGuardTests', '--nologo', '--no-build')
$JobPostFails = @(Get-FailLines $jobPostLog)
$JobClassification = ''
if ($JobPreExit -eq 0 -and $JobPostExit -eq 0) {
    $JobClassification = 'GREEN before and after'
} elseif ($JobPreExit -ne 0 -and $JobPostExit -ne 0 -and (($JobPreFails -join '|') -eq ($JobPostFails -join '|'))) {
    $JobClassification = 'INHERITED / PRE-EXISTING - identical before and after; not caused here, not fixed here'
} elseif ($JobPreExit -eq 0 -and $JobPostExit -ne 0) {
    Invoke-Rollback 'JobLog passes on the pristine tree and fails after apply: a regression'
} else {
    Invoke-Rollback ('JobLog failure set changed shape (pre ' + $JobPreFails.Count + ', post ' + $JobPostFails.Count + ')')
}
Write-Ok ("JobLog: " + $JobClassification)

Write-Step 'Phase 6c - stage, then self-check and repair whitespace this pack wrote'

# WHY THIS EXISTS. Every abort in this pack so far was whitespace hygiene found by
# git at the very last step, one defect per run. The pack now runs that check itself
# and repairs what it wrote.
#
# IT RUNS ON THE STAGED VIEW, not the working tree. "git diff --check" on the
# working tree of a CRLF checkout whose index is LF reports a bare CR as trailing
# whitespace on every added line. Repairing THAT would strip line endings across the
# file - the exact damage this pack already had to undo once. The staged view is the
# one the commit gate uses, so it is the one to satisfy.
#
# THE REPAIR ONLY EVER REMOVES SPACES AND TABS. It never removes a CR, never
# reformats, and never touches a path outside the owned manifest. Anything git still
# objects to after that is reported by name and stops the pack, because it is not
# this pack's whitespace.
function Add-OwnedPaths {
    foreach ($rel in $Script:OwnedPaths) {
        $null = Invoke-NativeLines -Exe 'git' -NativeArgs @('add', '--', $rel)
        if ($Script:LastNativeExit -ne 0) { Invoke-Rollback ("git add failed for " + $rel) }
    }
}

function Get-StagedWhitespaceFlags {
    return @(Invoke-NativeLines -Exe 'git' -NativeArgs (@('diff', '--cached', '--check', '--') + $Script:OwnedPaths) |
             Where-Object { $_ -match '^(.+?):(\d+): ' })
}

function Repair-StagedWhitespace {
    $flagged = @(Get-StagedWhitespaceFlags)
    if ($flagged.Count -eq 0) { return 0 }

    $byFile = @{}
    foreach ($line in $flagged) {
        if ($line -notmatch '^(.+?):(\d+): ') { continue }
        $file = ($Matches[1] -replace '\\', '/')
        $num  = [int]$Matches[2]
        if ($Script:OwnedPaths -notcontains $file) {
            Invoke-Rollback ("git flagged whitespace in a path this pack does not own: " + $file)
        }
        if (-not $byFile.ContainsKey($file)) { $byFile[$file] = New-Object System.Collections.ArrayList }
        [void]$byFile[$file].Add($num)
    }

    $repaired = 0
    foreach ($file in @($byFile.Keys)) {
        $full = ConvertTo-Live $file
        $raw = [System.IO.File]::ReadAllText($full)
        $lines = $raw -split "`n"

        foreach ($num in ($byFile[$file] | Sort-Object -Unique)) {
            $i = $num - 1
            if ($i -lt 0 -or $i -ge $lines.Count) { continue }

            $body = $lines[$i]
            $cr = ''
            if ($body.EndsWith("`r")) { $cr = "`r"; $body = $body.Substring(0, $body.Length - 1) }

            # Strip ANY trailing whitespace character, not just space and tab. A
            # previous run proved the flagged lines carry something else - the
            # earlier repair found nothing to remove and the gate still objected.
            # Guessing which character it is has cost runs; this removes every
            # trailing character the runtime calls whitespace, and never touches CR.
            $cut = $body.Length
            while ($cut -gt 0 -and [char]::IsWhiteSpace($body[$cut - 1])) { $cut-- }
            $trimmed = $body.Substring(0, $cut)

            if ($trimmed -eq $body) {
                # NOTHING TO STRIP, BUT THE LINE CARRIES A CR. git stores this file
                # with LF endings, so a stray CR on a line IS the trailing whitespace
                # it objects to - measured: "length 0, CR yes" on every flagged line.
                # This file's line endings are genuinely mixed; these few lines are
                # CRLF inside an LF file. Dropping the CR on a line git named makes
                # that line match the file it lives in. It is bounded to flagged
                # lines and never applied to the file as a whole - rewriting every
                # ending is the damage this pack already had to undo once.
                if ($cr -ne '') {
                    $lines[$i] = $trimmed
                    $repaired++
                    Write-Ok ("repaired stray CR in an LF file: " + $file + " line " + $num)
                    continue
                }

                $codes = @()
                foreach ($ch in $body.ToCharArray()) { $codes += ('U+{0:X4}' -f [int]$ch) }
                $tailCodes = @($codes | Select-Object -Last 12)
                Write-Bad ("   " + $file + " line " + $num + " has no trailing whitespace to strip.")
                Write-Bad ("      length " + $body.Length + ", CR no, last chars: " + ($tailCodes -join ' '))
                continue
            }

            $lines[$i] = $trimmed + $cr
            $repaired++
            Write-Ok ("repaired trailing whitespace: " + $file + " line " + $num + " (removed " + ($body.Length - $cut) + " char(s))")
        }

        [System.IO.File]::WriteAllText($full, ($lines -join "`n"), $Utf8NoBom)
    }

    return $repaired
}

Add-OwnedPaths
$RepairPasses = 0
$TotalRepaired = 0
while ($true) {
    $n = @(Repair-StagedWhitespace)[-1]
    if ($n -eq 0) { break }
    $TotalRepaired += $n
    $RepairPasses++
    Add-OwnedPaths
    if ($RepairPasses -ge 3) { Invoke-Rollback 'whitespace repair did not converge in three passes' }
}

$Remaining = @(Get-StagedWhitespaceFlags)
if ($Remaining.Count -ne 0) {
    foreach ($s in $Remaining) { Write-Bad ('   ' + $s) }

    # LAST RESORT DIAGNOSTIC. Print what git sees in the STAGED blob, not in the
    # working tree, because the two can differ by line-ending normalisation and
    # this pack has already been fooled by that once.
    foreach ($flag in $Remaining) {
        if ($flag -notmatch '^(.+?):(\d+): ') { continue }
        $dfile = ($Matches[1] -replace '\\', '/')
        $dnum = [int]$Matches[2]

        $stagedText = (@(Invoke-NativeLines -Exe 'git' -NativeArgs @('show', (':' + $dfile))) -join "`n")
        $stagedLines = $stagedText -split "`n"
        if ($dnum -ge 1 -and $dnum -le $stagedLines.Count) {
            $sl = $stagedLines[$dnum - 1]
            $codes = @()
            foreach ($ch in $sl.ToCharArray()) { $codes += ('U+{0:X4}' -f [int]$ch) }
            Write-Bad ("      STAGED " + $dfile + ':' + $dnum + " length " + $sl.Length + " last chars: " + ((@($codes | Select-Object -Last 12)) -join ' '))
        }
    }

    Invoke-Rollback ("git still objects to " + $Remaining.Count + " staged line(s) after repair. The character codes above say what is actually there.")
}
if ($TotalRepaired -eq 0) {
    Write-Ok 'staged view is whitespace-clean; nothing to repair'
} else {
    Write-Ok ("repaired " + $TotalRepaired + " line(s) in " + $RepairPasses + " pass(es); staged view now clean")
}

Write-Step 'Phase 6d - re-verify after repair'
if ($TotalRepaired -gt 0) {
    $reLog = Join-Path $BackupDir 'post-repair-build.log'
    $reExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $reLog -NativeArgs @('build', $TestProj, '--no-incremental', '--nologo')
    if ($reExit -ne 0) { Invoke-Rollback ("the whitespace repair broke the build; see " + $reLog) }
    $reTestLog = Join-Path $BackupDir 'post-repair-tests.log'
    $reTestExit = Invoke-NativeLogged -Exe 'dotnet' -LogPath $reTestLog -NativeArgs @(
        'test', $TestProj, '--filter', 'FullyQualifiedName~Dashboard|FullyQualifiedName~Widget|FullyQualifiedName~Dimension|FullyQualifiedName~DeclaredDimensionBindingTests', '--nologo', '--no-build')
    if ($reTestExit -ne 0) { foreach ($f in (Get-FailLines $reTestLog)) { Write-Bad ('   ' + $f) }; Invoke-Rollback ("suites failed after the whitespace repair; see " + $reTestLog) }
    Write-Ok 'build and suites still green after repair'
} else {
    Write-Ok 'nothing was repaired; no re-verification needed'
}

# ===========================================================================
# PHASE 7 - RESULT
# ===========================================================================
$r = New-Object System.Text.StringBuilder
[void]$r.AppendLine('T-094 WAVE A PART 2a - compiled plant dimensions retired')
[void]$r.AppendLine('')
[void]$r.AppendLine('mode                         : ' + $Mode)
[void]$r.AppendLine('parent SHA                   : ' + $HeadBefore)
[void]$r.AppendLine('branch                       : ' + $Branch)
[void]$r.AppendLine('anchored replacements        : 28, each matched exactly once')
[void]$r.AppendLine('retirement sweep             : 0 surviving compiled references')
[void]$r.AppendLine('owned-file warnings          : 0 new, ' + $Inherited.Count + ' inherited and unchanged')
[void]$r.AppendLine('declared binding             : GREEN')
[void]$r.AppendLine('genericity gate              : GREEN, zero unknown fingerprints')
[void]$r.AppendLine('UA-08 population             : ' + $Ua08Before + ' -> ' + $Ua08After + '  (' + $Removed + ' retired by this pack)')
[void]$r.AppendLine('inherited historical (49)    : unchanged at ' + $OtherBefore)
[void]$r.AppendLine('adjacent dashboard/widget    : GREEN')
[void]$r.AppendLine('JobLog                       : ' + $JobClassification)
[void]$r.AppendLine('whitespace repaired by pack  : ' + $TotalRepaired + ' line(s)')
[void]$r.AppendLine('')
[void]$r.AppendLine('UA-08 after this part: the removal is real in source; the baseline is regenerated')
[void]$r.AppendLine('with verdicts in wave C, which is where the closure arithmetic is stated.')
[void]$r.AppendLine('')
[void]$r.AppendLine('evidence                     : ' + $BackupDir)

if ($Mode -ne 'Commit') {
    Write-Step 'RESULT (Validate mode)'
    Write-Host $r.ToString()
    Write-Step 'Restoring owned paths - Validate mode leaves the repository as found'
    Invoke-Rollback 'Validate mode: gates ran, nothing is kept. Re-run with -Mode Commit to keep this change.'
}

Write-Step 'Phase 7 - exact staging'
Add-OwnedPaths
$Staged = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('diff', '--cached', '--name-only') | Where-Object { $_ -ne '' })
foreach ($s in $Staged) { Write-Host ("         " + $s) }
if ($Staged.Count -ne $Script:OwnedPaths.Count) { Invoke-Rollback ("staged " + $Staged.Count + " paths, expected " + $Script:OwnedPaths.Count) }
foreach ($s in $Staged) { if ($Script:OwnedPaths -notcontains ($s -replace '\\', '/')) { Invoke-Rollback ("unowned path in index: " + $s) } }
$CheckOut = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('diff', '--cached', '--check'))
foreach ($c in $CheckOut) { Write-Host ("         " + $c) }
if ($Script:LastNativeExit -ne 0) { Invoke-Rollback 'git diff --cached --check reported whitespace damage' }
$ForeignPreCommit = Get-ForeignFingerprint
if ($ForeignPreCommit -ne $ForeignBefore) { Invoke-Rollback ("foreign fingerprint changed: " + $ForeignBefore + " -> " + $ForeignPreCommit) }
Write-Ok 'index == owned manifest; --check GREEN; foreign preserved'

Write-Step 'Phase 7b - commit'
$Subject = 'T-094 retire compiled plant dimensions from the dashboard grammar'
$CommitOut = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('commit', '-q', '-m', $Subject))
foreach ($c in $CommitOut) { Write-Host ("         " + $c) }
if ($Script:LastNativeExit -ne 0) { Write-Bad 'git commit failed. Owned paths LEFT IN PLACE and staged.'; exit 4 }
$HeadAfter = @(Invoke-NativeLines -Exe 'git' -NativeArgs @('rev-parse', 'HEAD'))[0].Trim()
[void]$r.AppendLine('commit SHA                   : ' + $HeadAfter)
[void]$r.AppendLine('commit subject               : ' + $Subject)

$reportPath = Join-Path $EvidenceDir ('T-094_waveA_part2a_' + $Stamp + '.txt')
Write-AsciiFile -Path $reportPath -Content $r.ToString()
Write-Step 'RESULT'
Write-Host $r.ToString()
exit 0
