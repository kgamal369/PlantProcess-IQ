# Bind-DeclaredDimensionsAcrossSubjectLinks.ps1   (r3)
#
# T-094 part 2b, stage 1: a declared dimension whose source lives on a RELATED
# canonical entity becomes executable. Owner Worker 1. Release M2-P1.
#
# WHY THIS EXISTS. Part 1 made a declared dimension executable only when it binds
# to the subject entity itself; RequireBindable raises DB03 for anything else and
# its own comment defers the rest to "the path resolver that follows this task".
# The three compiled slots still standing - shift, risk class, defect - each
# filter through a DIFFERENT entity and intersect subject keys. Cutting them
# before this exists would delete filtering rather than generalise it.
#
# WHAT IT DOES. One structural fact, read from the model and never from a name:
#
#   for a source entity E and the subject entity S of the population, the mapped
#   single-column references from E to S are counted.
#     exactly one   -> that is the link. It is the model, not an inference.
#     none          -> DB06_subject_link_absent
#     more than one -> DB07_subject_link_ambiguous
#
# LAYERING, CORRECTED IN r3. r2 passed a DbContext into the Application layer and
# did not compile: the service holds IPlantProcessDbContext, which publishes named
# DbSets, a DatabaseFacade and SaveChanges - no Model and no Set<T>(). That is not
# an accident to work around. Model knowledge lives in Infrastructure, exactly
# where part 1 put SourceEntityType resolution. So the decision stays pure and
# testable in Application, and one governed resolver in Infrastructure reads the
# model and runs the query.
#
# ADDITIVE. No slot is removed, no wire contract changes, no fingerprint is
# retired. The tree is independently valid after this pack: declared filters that
# used to refuse with DB03 now execute, and everything else behaves identically.
# The cut lands in stage 2 together with the frontend.
#
# MODE. Default Validate: apply, run every gate, print the result, restore the
# owned paths and leave the repository exactly as found. -Mode Commit keeps and
# commits.
#
# Exit codes: 0 gates green. 2 refused before mutation. 3 a gate failed and the
#             owned paths were restored. 4 commit failed.

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
$BackupDir = Join-Path $BackupRoot ('t094b1_' + $Stamp)
$LogDir    = Join-Path $EvidenceDir ('t094b1_' + $Stamp)

function Write-Step { param([string] $Text) Write-Host ''; Write-Host ('=== ' + $Text + ' ===') }
function Write-Ok   { param([string] $Text) Write-Host ('  [ok]   ' + $Text) }
function Write-Warn { param([string] $Text) Write-Host ('  [warn] ' + $Text) }
function Write-Bad  { param([string] $Text) Write-Host ('  [BAD]  ' + $Text) }

function Stop-Refused {
    param([string] $Reason)
    Write-Bad ('REFUSED BEFORE MUTATION: ' + $Reason)
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
        $captured = @(& $Exe @NativeArgs 2>$null)
        $Script:LastNativeExit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previous
    }
    return @($captured | ForEach-Object { $_.ToString() })
}

function Get-TextSha256 {
    param([string] $Text)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') })
    } finally {
        $sha.Dispose()
    }
}

function Read-SourceText {
    param([string] $Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $text  = (New-Object System.Text.UTF8Encoding($false)).GetString($bytes)
    if ($text.Length -gt 0 -and [int]$text[0] -eq 0xFEFF) { $text = $text.Substring(1) }
    return $text
}

function Write-SourceText {
    param([string] $Path, [string] $Text)
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $encoding)
}

# ---------------------------------------------------------------------------
# ROLLBACK (owned paths only; foreign dirt is never touched)
# ---------------------------------------------------------------------------
$Script:Applied = $false

function Invoke-Rollback {
    param([string] $Reason)

    Write-Bad ('FAILURE: ' + $Reason)

    if (-not $Script:Applied) {
        Write-Ok 'nothing was applied; the tree was never mutated'
        exit 3
    }

    Write-Step 'AUTOMATIC ROLLBACK of owned paths'

    foreach ($relative in $Script:OwnedPaths) {
        $full   = Join-Path $RepoRoot ($relative -replace '/', '\')
        $backup = Join-Path $BackupDir (($relative -replace '[\\/]', '__'))

        if (Test-Path -LiteralPath $backup) {
            Copy-Item -LiteralPath $backup -Destination $full -Force
            (Get-Item -LiteralPath $full).LastWriteTime = Get-Date
            Write-Ok ('restored ' + $relative)
        } elseif (Test-Path -LiteralPath $full) {
            Remove-Item -LiteralPath $full -Force
            Write-Ok ('removed created file ' + $relative)
        }
    }

    Push-Location $RepoRoot
    try {
        foreach ($relative in $Script:OwnedPaths) {
            [void](Invoke-NativeLines 'git' @('reset', '--quiet', 'HEAD', '--', $relative))
        }
        # W1-PACK-ARGSPLAT-01. The argument vector is built BEFORE the call. Written
        # as @(...) + $paths in argument position, PowerShell passes the '+' and the
        # second array as separate arguments, git receives no pathspec at all, and
        # the whole repository is reported instead of the owned paths.
        $dirtyArgs = @('status', '--porcelain', '--untracked-files=all', '--') + $Script:OwnedPaths
        $dirty = @(Invoke-NativeLines 'git' $dirtyArgs)
        Write-Ok ('owned paths dirty after rollback : ' + $dirty.Count)
        $head = @(Invoke-NativeLines 'git' @('rev-parse', 'HEAD'))
        Write-Ok ('HEAD after rollback              : ' + $head[0])
    } finally {
        Pop-Location
    }

    exit 3
}

trap {
    if ($Script:Applied) { Invoke-Rollback ('unhandled error: ' + $_.Exception.Message) }
    Write-Bad ('unhandled error before mutation: ' + $_.Exception.Message)
    exit 2
}

# ---------------------------------------------------------------------------
# OWNED MANIFEST
# ---------------------------------------------------------------------------
$Script:OwnedPaths = @(
    'Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DimensionBindingRefusalException.cs'
    'Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DeclaredDimensionProjection.cs'
    'Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/IDeclaredDimensionSubjectLinkResolver.cs'
    'Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardWidgetQueryService.cs'
    'Backend/PlantProcess.Infrastructure/Dashboarding/Dimensions/DeclaredDimensionSubjectLinkResolver.cs'
    'Backend/PlantProcess.Infrastructure/DependencyInjection.cs'
    'Backend/tests/PlantProcess.Architecture.Tests/DeclaredDimensionSubjectLinkTests.cs'
)

$Script:CreatedPaths = @(
    'Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/IDeclaredDimensionSubjectLinkResolver.cs'
    'Backend/PlantProcess.Infrastructure/Dashboarding/Dimensions/DeclaredDimensionSubjectLinkResolver.cs'
    'Backend/tests/PlantProcess.Architecture.Tests/DeclaredDimensionSubjectLinkTests.cs'
)

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
New-Item -ItemType Directory -Force -Path $LogDir    | Out-Null

# ===========================================================================
# PHASE 0 - REPOSITORY STATE
# ===========================================================================
Write-Step ('Phase 0 - repository state (mode: ' + $Mode + ')')

if (-not (Test-Path -LiteralPath $RepoRoot)) { Stop-Refused ('repository root not found: ' + $RepoRoot) }
Push-Location $RepoRoot

$head = @(Invoke-NativeLines 'git' @('rev-parse', 'HEAD'))
if ($Script:LastNativeExit -ne 0 -or $head.Count -eq 0) { Pop-Location; Stop-Refused 'git rev-parse HEAD failed' }
Write-Ok ('HEAD   : ' + $head[0])

$branch = @(Invoke-NativeLines 'git' @('rev-parse', '--abbrev-ref', 'HEAD'))
Write-Ok ('branch : ' + $branch[0])

$staged = @(Invoke-NativeLines 'git' @('diff', '--cached', '--name-only'))
if ($staged.Count -gt 0) {
    Pop-Location
    Stop-Refused ('the index is not empty (' + $staged.Count + ' path(s)); exact staging requires an empty index')
}
Write-Ok 'index is empty'

[void](Invoke-NativeLines 'git' @('merge-base', '--is-ancestor', '4d32c6b10b4605aeb6e0705ad03537d563125652', 'HEAD'))
if ($Script:LastNativeExit -ne 0) {
    Pop-Location
    Stop-Refused 'the frozen T-244 commit is not an ancestor of HEAD'
}
Write-Ok 'T-244 authority is an ancestor of HEAD'

$allDirty = @(Invoke-NativeLines 'git' @('status', '--porcelain', '--untracked-files=all'))
$foreign = @($allDirty | Where-Object {
    $line = $_.TrimEnd("`r")
    if ($line.Length -lt 4) { return $false }
    $path = $line.Substring(3).Trim().Trim('"')
    return -not ($Script:OwnedPaths -contains $path)
})
$Script:ForeignBefore = $foreign.Count
$Script:ForeignFingerprint = (Get-TextSha256 -Text (($foreign | Sort-Object) -join "`n")).Substring(0, 16).ToUpper()
Write-Ok ('foreign fingerprint : ' + $Script:ForeignFingerprint + ' (' + $Script:ForeignBefore + ' entries)')

Pop-Location

# ===========================================================================
# PHASE 1 - PRECONDITIONS
# ===========================================================================
Write-Step 'Phase 1 - preconditions'

$ProjectionPath = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Services\Dimensions\DeclaredDimensionProjection.cs'
$RefusalPath    = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Services\Dimensions\DimensionBindingRefusalException.cs'
$ResolverIfPath = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Services\Dimensions\IDeclaredDimensionSubjectLinkResolver.cs'
$ServicePath    = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Services\Queries\DashboardWidgetQueryService.cs'
$ResolverPath   = Join-Path $RepoRoot 'Backend\PlantProcess.Infrastructure\Dashboarding\Dimensions\DeclaredDimensionSubjectLinkResolver.cs'
$DiPath         = Join-Path $RepoRoot 'Backend\PlantProcess.Infrastructure\DependencyInjection.cs'
$TestPath       = Join-Path $RepoRoot 'Backend\tests\PlantProcess.Architecture.Tests\DeclaredDimensionSubjectLinkTests.cs'

foreach ($p in @($ProjectionPath, $RefusalPath, $ServicePath, $DiPath)) {
    if (-not (Test-Path -LiteralPath $p)) { Stop-Refused ('required source not found: ' + $p) }
}
foreach ($p in @($ResolverIfPath, $ResolverPath, $TestPath)) {
    if (Test-Path -LiteralPath $p) { Stop-Refused ('a file this pack creates already exists: ' + $p) }
}

$projectionText = Read-SourceText -Path $ProjectionPath
$refusalText    = Read-SourceText -Path $RefusalPath
$diText         = Read-SourceText -Path $DiPath

if ($projectionText -notmatch 'WhereDeclaredEquals') { Stop-Refused 'part 1 binding contract is missing WhereDeclaredEquals' }
if ($projectionText -match 'SelectSubjectLinkField') { Stop-Refused 'stage 1 appears to be applied already' }
if ($refusalText -match 'SubjectLinkAbsent') { Stop-Refused 'stage 1 refusal codes are already present' }
if ($diText -notmatch 'IDeclaredDimensionCatalog') { Stop-Refused 'the part 1 catalogue is not composed; stage 1 registers beside it' }
Write-Ok 'part 1 contract present and composed; stage 1 not yet applied'

$existingOwned = @(
    'Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DimensionBindingRefusalException.cs'
    'Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DeclaredDimensionProjection.cs'
    'Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardWidgetQueryService.cs'
    'Backend/PlantProcess.Infrastructure/DependencyInjection.cs'
)
Push-Location $RepoRoot
$ownedStatusArgs = @('status', '--porcelain', '--untracked-files=all', '--') + $existingOwned
$ownedDirty = @(Invoke-NativeLines 'git' $ownedStatusArgs)
Pop-Location
if ($ownedDirty.Count -gt 0) { Stop-Refused ('owned paths are not clean: ' + ($ownedDirty -join '; ')) }
Write-Ok 'owned paths are clean'

# ===========================================================================
# PHASE 2 - PRISTINE BASELINE
# ===========================================================================
Write-Step 'Phase 2 - pristine baseline'

$TestProject = Join-Path $RepoRoot 'Backend\tests\PlantProcess.Architecture.Tests'

function Invoke-TestFilter {
    param([string] $Filter, [string] $LogName)
    $log = Join-Path $LogDir $LogName
    $code = Invoke-NativeLogged 'dotnet' @('test', $TestProject, '--filter', $Filter, '-v', 'q', '--nologo') $log
    if (-not (Test-Path -LiteralPath $log)) { return @{ Exit = 99; Passed = 0; Failed = 0; Total = 0 } }
    if ((Get-Item -LiteralPath $log).Length -eq 0) { return @{ Exit = 98; Passed = 0; Failed = 0; Total = 0 } }

    $passed = 0; $failed = 0; $total = 0
    foreach ($line in (Get-Content -LiteralPath $log)) {
        if ($line -match 'Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)') {
            $failed = [int]$Matches[1]; $passed = [int]$Matches[2]; $total = [int]$Matches[4]
        } elseif ($line -match 'Passed!.*Passed:\s+(\d+).*Total:\s+(\d+)') {
            $passed = [int]$Matches[1]; $total = [int]$Matches[2]
        }
    }
    return @{ Exit = $code; Passed = $passed; Failed = $failed; Total = $total }
}

$jobLogBefore = Invoke-TestFilter -Filter 'FullyQualifiedName~JobLogObservabilitySourceGuardTests' -LogName 'joblog-before.log'
Write-Ok ('JobLog pristine : exit ' + $jobLogBefore.Exit + ', failing ' + $jobLogBefore.Failed + ', total ' + $jobLogBefore.Total)

$bindingBefore = Invoke-TestFilter -Filter 'FullyQualifiedName~DeclaredDimensionBindingTests' -LogName 'binding-before.log'
if ($bindingBefore.Failed -ne 0 -or $bindingBefore.Total -eq 0) {
    Stop-Refused ('the part 1 binding suite is not green before this pack (failed ' + $bindingBefore.Failed + ', total ' + $bindingBefore.Total + ')')
}
Write-Ok ('part 1 binding suite pristine : ' + $bindingBefore.Passed + '/' + $bindingBefore.Total)

$ua08Before = Invoke-TestFilter -Filter 'FullyQualifiedName~Genericity' -LogName 'genericity-before.log'
if ($ua08Before.Failed -ne 0) {
    Stop-Refused 'the genericity gate is not green before this pack; stage 1 must start from a green ratchet'
}
Write-Ok ('genericity gate pristine : ' + $ua08Before.Passed + '/' + $ua08Before.Total)

# ===========================================================================
# PHASE 3 - BACKUP
# ===========================================================================
Write-Step 'Phase 3 - exact owned backups'

foreach ($relative in $Script:OwnedPaths) {
    if ($Script:CreatedPaths -contains $relative) { continue }
    $full   = Join-Path $RepoRoot ($relative -replace '/', '\')
    $backup = Join-Path $BackupDir (($relative -replace '[\\/]', '__'))
    Copy-Item -LiteralPath $full -Destination $backup -Force
    Write-Ok ('backed up ' + $relative)
}

# ===========================================================================
# PHASE 4 - APPLY
# ===========================================================================
Write-Step 'Phase 4 - apply'

$Script:Applied = $true

function Invoke-AnchoredEdit {
    param([string] $Path, [string] $Label, [string] $Old, [string] $New)

    $text = Read-SourceText -Path $Path
    $crlf = $text.Contains("`r`n")
    $flat = $text -replace "`r`n", "`n"
    $oldFlat = $Old -replace "`r`n", "`n"
    $newFlat = $New -replace "`r`n", "`n"

    $first = $flat.IndexOf($oldFlat, [System.StringComparison]::Ordinal)
    if ($first -lt 0) { Invoke-Rollback ('anchor not found: ' + $Label) }
    $second = $flat.IndexOf($oldFlat, $first + 1, [System.StringComparison]::Ordinal)
    if ($second -ge 0) { Invoke-Rollback ('anchor matched more than once: ' + $Label) }

    $flat = $flat.Substring(0, $first) + $newFlat + $flat.Substring($first + $oldFlat.Length)
    if ($crlf) { $flat = $flat -replace "`n", "`r`n" }
    Write-SourceText -Path $Path -Text $flat
    Write-Ok ('applied: ' + $Label)
}

function New-SourceFile {
    param([string] $Path, [string] $Label, [string] $Body)
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    $text = $Body -replace "`r`n", "`n"
    $text = $text -replace "`n", "`r`n"
    Write-SourceText -Path $Path -Text $text
    Write-Ok ('created: ' + $Label)
}

# --- edit 1: two structural refusal codes -----------------------------------
$refusalOld = @'
    public const string TenantUnresolved = "DB05_tenant_unresolved";
'@

$refusalNew = @'
    public const string TenantUnresolved = "DB05_tenant_unresolved";

    /// <summary>
    /// The declaration binds to a canonical entity that carries no mapped single-column
    /// reference to the subject entity of this population. There is no path, so there is
    /// no answer; inventing a join would invent a number.
    /// </summary>
    public const string SubjectLinkAbsent = "DB06_subject_link_absent";

    /// <summary>
    /// The declaration's entity references the subject entity through more than one
    /// mapped reference. Choosing one would be a guess about meaning, so the engine
    /// refuses and the declaration must state which relationship it means.
    /// </summary>
    public const string SubjectLinkAmbiguous = "DB07_subject_link_ambiguous";

    /// <summary>
    /// The declaration is executable but this composition carries no resolver able to
    /// reach a related entity. A capability that is absent is reported as absent.
    /// </summary>
    public const string SubjectLinkUnavailable = "DB08_subject_link_unavailable";
'@

Invoke-AnchoredEdit -Path $RefusalPath -Label 'refusal codes: subject link absent, ambiguous, unavailable' -Old $refusalOld -New $refusalNew

# --- edit 2: the projection gains the pure half of the decision --------------
$projectionOld = @'
    public static void RequireBindable(DeclaredDimension declared, Type sourceEntityType)
'@

$projectionNew = @'
    /// <summary>
    /// True when the declaration binds to the subject entity itself and can therefore
    /// restrict the subject population directly.
    /// </summary>
    public static bool BindsToSubject(DeclaredDimension declared, Type subjectEntityType) =>
        declared is not null &&
        declared.IsBindable &&
        declared.SourceEntityType is not null &&
        declared.SourceEntityType == subjectEntityType;

    /// <summary>
    /// Choose the single reference that links a source entity to the subject entity of
    /// a population. The decision is arithmetic over what the model declares: one
    /// candidate is the link, none is a refusal, several is a refusal. No name is
    /// compared, so a customer concept published against any related entity resolves by
    /// the same rule that resolves every other one.
    ///
    /// The candidates are supplied by the caller. Reading them is model knowledge and
    /// belongs to the persistence layer; deciding what they mean is a contract and
    /// belongs here, where it can be falsified without a database.
    /// </summary>
    public static string SelectSubjectLinkField(string dimensionCode, IReadOnlyList<string> candidateFields)
    {
        ArgumentNullException.ThrowIfNull(candidateFields);

        if (candidateFields.Count == 0)
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.SubjectLinkAbsent,
                dimensionCode,
                "Declared dimension '" + dimensionCode + "' is published against an entity that carries no " +
                "mapped reference to the subject of this population. No relationship is inferred.");
        }

        if (candidateFields.Count > 1)
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.SubjectLinkAmbiguous,
                dimensionCode,
                "Declared dimension '" + dimensionCode + "' is published against an entity that references the " +
                "subject of this population through " + candidateFields.Count + " mapped references. " +
                "The declaration must state which one it means.");
        }

        return candidateFields[0];
    }

    /// <summary>
    /// The subject keys whose related row carries the declared value: restrict the
    /// related population by the declared field, then project its reference to the
    /// subject. This is the generic form of the shape the compiled slots used, and it
    /// spells no entity, no column and no concept.
    /// </summary>
    public static IQueryable<Guid> SubjectKeys<TSource>(
        IQueryable<TSource> source,
        DeclaredDimension declared,
        string linkField,
        bool optionalLink,
        string value)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(declared);
        if (string.IsNullOrWhiteSpace(linkField)) { throw new ArgumentException("A link field is required.", nameof(linkField)); }

        var restricted = WhereDeclaredEquals(source, declared, value ?? string.Empty);
        var parameter = Expression.Parameter(typeof(TSource), "x");

        if (!optionalLink)
        {
            var selector = Expression.Lambda<Func<TSource, Guid>>(
                Expression.Call(EfPropertyOfGuid, parameter, Expression.Constant(linkField, typeof(string))),
                parameter);

            return restricted.Select(selector).Distinct();
        }

        var access = Expression.Call(EfPropertyOfNullableGuid, parameter, Expression.Constant(linkField, typeof(string)));

        var present = Expression.Lambda<Func<TSource, bool>>(
            Expression.Property(access, "HasValue"), parameter);

        var carried = Expression.Lambda<Func<TSource, Guid>>(
            Expression.Property(access, "Value"), parameter);

        return restricted.Where(present).Select(carried).Distinct();
    }

    /// <summary>
    /// The publication half of RequireBindable: is this declaration executable at all,
    /// independently of which population is being restricted.
    /// </summary>
    public static void RequireBindableAnywhere(DeclaredDimension declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        if (!declared.IsBindable || declared.SourceEntityType is null)
        {
            throw new DimensionBindingRefusalException(
                declared.BindingRefusalCode ?? DimensionBindingRefusalCodes.Unbindable,
                declared.Code,
                "Declared dimension '" + declared.Code + "' is published but not executable: " +
                (declared.BindingRefusalReason ?? "its source binding could not be resolved against the canonical model."));
        }
    }

    public static void RequireBindable(DeclaredDimension declared, Type sourceEntityType)
'@

Invoke-AnchoredEdit -Path $ProjectionPath -Label 'projection: subject-link decision and key selection' -Old $projectionOld -New $projectionNew

$usingsOld = @'
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
'@

$usingsNew = @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
'@

Invoke-AnchoredEdit -Path $ProjectionPath -Label 'projection: usings' -Old $usingsOld -New $usingsNew

$reflectionOld = @'
    private static readonly MethodInfo EfPropertyOfString =
        typeof(EF).GetMethod(nameof(EF.Property), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeof(string));
'@

$reflectionNew = @'
    private static readonly MethodInfo EfPropertyOfString =
        typeof(EF).GetMethod(nameof(EF.Property), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeof(string));

    private static readonly MethodInfo EfPropertyOfGuid =
        typeof(EF).GetMethod(nameof(EF.Property), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeof(Guid));

    private static readonly MethodInfo EfPropertyOfNullableGuid =
        typeof(EF).GetMethod(nameof(EF.Property), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeof(Guid?));
'@

Invoke-AnchoredEdit -Path $ProjectionPath -Label 'projection: reflection handles' -Old $reflectionOld -New $reflectionNew

# --- create: the Application-side capability contract ------------------------
$resolverInterface = @'
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlantProcess.Application.Dashboarding.Services.Dimensions;

/// <summary>
/// Resolves a declared dimension that is published against a RELATED canonical entity
/// into the subject keys its value selects.
///
/// The Application layer holds no model metadata: IPlantProcessDbContext publishes
/// named sets, a database facade and SaveChanges, and nothing that could answer "which
/// reference links these two entities". That question is persistence knowledge and its
/// answer lives behind this contract, exactly as the declared-dimension catalogue holds
/// the answer to "which entity and member does this declaration bind to".
///
/// The implementation decides nothing on its own: it reads the model, hands the
/// candidates to DeclaredDimensionProjection.SelectSubjectLinkField, and executes the
/// projection that class builds. A refusal is a typed refusal, never an empty set.
/// </summary>
public interface IDeclaredDimensionSubjectLinkResolver
{
    Task<IReadOnlyList<Guid>> SubjectKeysWhereDeclaredEqualsAsync(
        DeclaredDimension declared,
        Type subjectEntityType,
        string value,
        CancellationToken cancellationToken);
}
'@

New-SourceFile -Path $ResolverIfPath -Label 'IDeclaredDimensionSubjectLinkResolver.cs' -Body $resolverInterface

# --- create: the Infrastructure implementation -------------------------------
$resolverSource = @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Dashboarding.Dimensions;

/// <summary>
/// Reads the model to find how a declaration's entity reaches the subject entity of a
/// population, and executes the resulting projection.
///
/// Governance is structural, not lexical. The candidates are the mapped single-column
/// references from the declaration's entity to the subject entity, with shadow members
/// and non-identity members excluded. Which of them is the link is decided by the
/// Application contract, not here; this class supplies facts and runs the query.
/// </summary>
public sealed class DeclaredDimensionSubjectLinkResolver : IDeclaredDimensionSubjectLinkResolver
{
    private static readonly MethodInfo ReadKeysMethod =
        typeof(DeclaredDimensionSubjectLinkResolver)
            .GetMethod(nameof(ReadSubjectKeysAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly PlantProcessDbContext _db;

    public DeclaredDimensionSubjectLinkResolver(PlantProcessDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Guid>> SubjectKeysWhereDeclaredEqualsAsync(
        DeclaredDimension declared,
        Type subjectEntityType,
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(subjectEntityType);

        DeclaredDimensionProjection.RequireBindableAnywhere(declared);

        var sourceEntityType = declared.SourceEntityType!;
        var entity = _db.Model.FindEntityType(sourceEntityType);

        if (entity is null)
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.Unbindable,
                declared.Code,
                "Declared dimension '" + declared.Code + "' binds to " + sourceEntityType.Name +
                ", which is not a mapped canonical entity.");
        }

        var candidates = entity.GetForeignKeys()
            .Where(fk => fk.PrincipalEntityType.ClrType == subjectEntityType)
            .Where(fk => fk.Properties.Count == 1)
            .Select(fk => fk.Properties[0])
            .Where(p => !p.IsShadowProperty())
            .Where(p => p.ClrType == typeof(Guid) || p.ClrType == typeof(Guid?))
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var linkField = DeclaredDimensionProjection.SelectSubjectLinkField(declared.Code, candidates);
        var optional = entity.FindProperty(linkField)!.ClrType == typeof(Guid?);

        var task = (Task<IReadOnlyList<Guid>>)ReadKeysMethod
            .MakeGenericMethod(sourceEntityType)
            .Invoke(this, new object[] { declared, linkField, optional, value ?? string.Empty, cancellationToken })!;

        return await task;
    }

    private async Task<IReadOnlyList<Guid>> ReadSubjectKeysAsync<TSource>(
        DeclaredDimension declared,
        string linkField,
        bool optionalLink,
        string value,
        CancellationToken cancellationToken)
        where TSource : class
    {
        var keys = DeclaredDimensionProjection.SubjectKeys(
            _db.Set<TSource>().AsNoTracking(),
            declared,
            linkField,
            optionalLink,
            value);

        return await keys.ToListAsync(cancellationToken);
    }
}
'@

New-SourceFile -Path $ResolverPath -Label 'DeclaredDimensionSubjectLinkResolver.cs' -Body $resolverSource

# --- edit 3: compose the resolver beside the catalogue -----------------------
$diOld = @'
        services.AddScoped<PlantProcess.Application.Dashboarding.Services.Dimensions.IDeclaredDimensionCatalog,
            PlantProcess.Infrastructure.Dashboarding.Dimensions.DeclaredDimensionCatalog>();
'@

$diNew = @'
        services.AddScoped<PlantProcess.Application.Dashboarding.Services.Dimensions.IDeclaredDimensionCatalog,
            PlantProcess.Infrastructure.Dashboarding.Dimensions.DeclaredDimensionCatalog>();

        // T-094. Subject-link resolver: a declaration published against a related
        // canonical entity is reached through the single mapped reference that entity
        // carries to the subject of the population. Composed here because this project
        // owns model metadata; the decision itself stays in the Application contract.
        services.AddScoped<PlantProcess.Application.Dashboarding.Services.Dimensions.IDeclaredDimensionSubjectLinkResolver,
            PlantProcess.Infrastructure.Dashboarding.Dimensions.DeclaredDimensionSubjectLinkResolver>();
'@

Invoke-AnchoredEdit -Path $DiPath -Label 'composition: subject-link resolver beside the catalogue' -Old $diOld -New $diNew

# --- edit 4: the query service takes the capability --------------------------
$fieldOld = @'
    private readonly IDeclaredDimensionCatalog? _declaredDimensions;
'@

$fieldNew = @'
    private readonly IDeclaredDimensionCatalog? _declaredDimensions;
    private readonly IDeclaredDimensionSubjectLinkResolver? _subjectLinkResolver;
'@

Invoke-AnchoredEdit -Path $ServicePath -Label 'service: subject-link capability field' -Old $fieldOld -New $fieldNew

$ctorOld = @'
        IDeclaredDimensionCatalog? declaredDimensions = null)
    {
        _dbContext = dbContext;
        _validationService = validationService;
        _tenantAccessor = tenantAccessor;
        _evidenceWriter = evidenceWriter;
        _declaredDimensions = declaredDimensions;
'@

$ctorNew = @'
        IDeclaredDimensionCatalog? declaredDimensions = null,
        IDeclaredDimensionSubjectLinkResolver? subjectLinkResolver = null)
    {
        _dbContext = dbContext;
        _validationService = validationService;
        _tenantAccessor = tenantAccessor;
        _evidenceWriter = evidenceWriter;
        _declaredDimensions = declaredDimensions;
        _subjectLinkResolver = subjectLinkResolver;
'@

Invoke-AnchoredEdit -Path $ServicePath -Label 'service: optional subject-link dependency' -Old $ctorOld -New $ctorNew

$serviceOld = @'
        // T-094. Declared-dimension filters, keyed by published code. Each binds to
        // the material population through the same contract the grouping uses; a
        // declaration that does not bind here is a typed refusal, never a guess.
        if (filters?.DimensionFilters is { Count: > 0 })
        {
            foreach (var dimensionFilter in filters.DimensionFilters)
            {
                if (dimensionFilter is null || string.IsNullOrWhiteSpace(dimensionFilter.Code)) continue;

                var declaredFilter = await RequireDeclaredDimensionAsync(dimensionFilter.Code, cancellationToken);
                query = DeclaredDimensionProjection.WhereDeclaredEquals(query, declaredFilter, dimensionFilter.Value ?? string.Empty);
            }
        }
'@

$serviceNew = @'
        // T-094. Declared-dimension filters, keyed by published code. A declaration
        // published against the subject entity restricts this population directly. One
        // published against a related canonical entity is reached through the single
        // mapped reference that entity carries to the subject, and intersected below -
        // the same shape the compiled slots used, decided by the model rather than by a
        // name. Absent or ambiguous references are typed refusals, never a guess.
        var subjectEntityType = query.ElementType;
        var relatedDeclaredFilters = new List<(DeclaredDimension Declared, string Value)>();

        if (filters?.DimensionFilters is { Count: > 0 })
        {
            foreach (var dimensionFilter in filters.DimensionFilters)
            {
                if (dimensionFilter is null || string.IsNullOrWhiteSpace(dimensionFilter.Code)) continue;

                var declaredFilter = await RequireDeclaredDimensionAsync(dimensionFilter.Code, cancellationToken);
                var declaredValue = dimensionFilter.Value ?? string.Empty;

                if (DeclaredDimensionProjection.BindsToSubject(declaredFilter, subjectEntityType))
                {
                    query = DeclaredDimensionProjection.WhereDeclaredEquals(query, declaredFilter, declaredValue);
                }
                else
                {
                    relatedDeclaredFilters.Add((declaredFilter, declaredValue));
                }
            }
        }
'@

Invoke-AnchoredEdit -Path $ServicePath -Label 'service: route declared filters by binding entity' -Old $serviceOld -New $serviceNew

$intersectOld = @'
        var result = materialIds.ToHashSet();
'@

$intersectNew = @'
        var result = materialIds.ToHashSet();

        foreach (var relatedFilter in relatedDeclaredFilters)
        {
            if (_subjectLinkResolver is null)
            {
                throw new DimensionBindingRefusalException(
                    DimensionBindingRefusalCodes.SubjectLinkUnavailable,
                    relatedFilter.Declared.Code,
                    "Declared dimension '" + relatedFilter.Declared.Code + "' is published against a related " +
                    "entity, and this composition carries no subject-link resolver to reach it.");
            }

            var linkedKeys = await _subjectLinkResolver.SubjectKeysWhereDeclaredEqualsAsync(
                relatedFilter.Declared,
                subjectEntityType,
                relatedFilter.Value,
                cancellationToken);

            result.IntersectWith(linkedKeys);
        }
'@

Invoke-AnchoredEdit -Path $ServicePath -Label 'service: intersect related declared populations' -Old $intersectOld -New $intersectNew

# --- create: the stage 1 proof ----------------------------------------------
$testSource = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-094 part 2b, stage 1: a declaration published against a related canonical entity
/// becomes executable through the single mapped reference that entity carries to the
/// subject of the population.
///
/// The decision proved here is arithmetic over what the model declares - one candidate
/// binds, none refuses, several refuse. No name is compared anywhere, so a customer
/// concept published against any related entity resolves by the same rule. Reading the
/// model and translating the query is the resolver's work and is proved against a real
/// database by the execution falsification, not here.
/// </summary>
[Trait("BacklogTask", "T-094")]
[Trait("Gate", "DeclaredDimensionSubjectLink")]
public sealed class DeclaredDimensionSubjectLinkTests
{
    private sealed class SubjectRow
    {
        public Guid Id { get; set; }
    }

    private sealed class RelatedRow
    {
        public Guid Id { get; set; }
        public Guid SubjectRef { get; set; }
        public string? Bucket { get; set; }
    }

    private static DeclaredDimension Bindable(string code, Type entity, string field) =>
        new(code, code, "string", "unit", entity.Name, field, entity, true, null, null, Guid.NewGuid(), 1);

    private static DeclaredDimension Unbindable(string code) =>
        new(code, code, "string", "unit", "NoSuchEntity", "NoSuchField",
            null, false, DimensionBindingRefusalCodes.Unbindable, "no such entity", Guid.NewGuid(), 2);

    [Fact]
    public void A_declaration_on_the_subject_itself_is_recognised_as_directly_bindable()
    {
        Assert.True(DeclaredDimensionProjection.BindsToSubject(
            Bindable("a", typeof(SubjectRow), "Bucket"), typeof(SubjectRow)));
    }

    [Fact]
    public void A_declaration_on_a_related_entity_is_not_directly_bindable()
    {
        Assert.False(DeclaredDimensionProjection.BindsToSubject(
            Bindable("b", typeof(RelatedRow), "Bucket"), typeof(SubjectRow)));
    }

    [Fact]
    public void An_unbindable_declaration_never_claims_to_bind_to_the_subject()
    {
        Assert.False(DeclaredDimensionProjection.BindsToSubject(Unbindable("ghost"), typeof(SubjectRow)));
    }

    [Fact]
    public void Exactly_one_mapped_reference_is_the_link()
    {
        Assert.Equal(
            "SubjectRef",
            DeclaredDimensionProjection.SelectSubjectLinkField("c", new List<string> { "SubjectRef" }));
    }

    [Fact]
    public void No_reference_to_the_subject_is_a_typed_refusal_rather_than_an_empty_result()
    {
        var refusal = Assert.Throws<DimensionBindingRefusalException>(() =>
            DeclaredDimensionProjection.SelectSubjectLinkField("d", Array.Empty<string>()));

        Assert.Equal(DimensionBindingRefusalCodes.SubjectLinkAbsent, refusal.RefusalCode);
        Assert.Equal("d", refusal.DimensionCode);
    }

    [Fact]
    public void More_than_one_reference_is_refused_rather_than_chosen()
    {
        var refusal = Assert.Throws<DimensionBindingRefusalException>(() =>
            DeclaredDimensionProjection.SelectSubjectLinkField("e", new List<string> { "FirstRef", "SecondRef" }));

        Assert.Equal(DimensionBindingRefusalCodes.SubjectLinkAmbiguous, refusal.RefusalCode);
        Assert.Contains("2", refusal.Message);
    }

    [Fact]
    public void A_published_but_unexecutable_declaration_is_refused_before_any_link_is_sought()
    {
        var refusal = Assert.Throws<DimensionBindingRefusalException>(() =>
            DeclaredDimensionProjection.RequireBindableAnywhere(Unbindable("ghost")));

        Assert.Equal(DimensionBindingRefusalCodes.Unbindable, refusal.RefusalCode);
        Assert.Contains("no such entity", refusal.Message);
    }

    [Fact]
    public void The_key_projection_reads_the_declared_field_and_the_link_field_as_data()
    {
        var rows = new[]
        {
            new RelatedRow { Id = Guid.NewGuid(), SubjectRef = Guid.NewGuid(), Bucket = "b1" },
            new RelatedRow { Id = Guid.NewGuid(), SubjectRef = Guid.NewGuid(), Bucket = "b2" },
        }.AsQueryable();

        var keys = DeclaredDimensionProjection.SubjectKeys(
            rows, Bindable("f", typeof(RelatedRow), "Bucket"), "SubjectRef", false, "b2");

        var text = keys.Expression.ToString();
        Assert.Contains("Bucket", text);
        Assert.Contains("SubjectRef", text);
        Assert.Contains("Distinct", text);
    }

    [Fact]
    public void The_structural_refusal_codes_are_distinct_and_stated()
    {
        Assert.Equal("DB06_subject_link_absent", DimensionBindingRefusalCodes.SubjectLinkAbsent);
        Assert.Equal("DB07_subject_link_ambiguous", DimensionBindingRefusalCodes.SubjectLinkAmbiguous);
        Assert.Equal("DB08_subject_link_unavailable", DimensionBindingRefusalCodes.SubjectLinkUnavailable);
        Assert.NotEqual(DimensionBindingRefusalCodes.SubjectLinkAbsent, DimensionBindingRefusalCodes.SourceMismatch);
    }

    [Fact]
    public void The_resolver_is_composed_so_a_related_declaration_is_reachable_at_runtime()
    {
        // A capability that is registered nowhere refuses every query it was built to
        // answer, and no unit test of the contract would notice.
        var composition = File.ReadAllText(Path.Combine(
            ScopeAwareGenericity.RepositoryRoot(),
            "Backend", "PlantProcess.Infrastructure", "DependencyInjection.cs"));

        Assert.Contains("IDeclaredDimensionCatalog", composition, StringComparison.Ordinal);
        Assert.Contains("IDeclaredDimensionSubjectLinkResolver", composition, StringComparison.Ordinal);
    }
}
'@

New-SourceFile -Path $TestPath -Label 'DeclaredDimensionSubjectLinkTests.cs' -Body $testSource

# ===========================================================================
# PHASE 5 - SELF CHECK ON DISK
# ===========================================================================
Write-Step 'Phase 5 - self-check on disk'

$projectionAfter = Read-SourceText -Path $ProjectionPath
foreach ($required in @('BindsToSubject', 'SelectSubjectLinkField', 'SubjectKeys<', 'RequireBindableAnywhere', 'EfPropertyOfNullableGuid')) {
    if ($projectionAfter -notmatch [regex]::Escape($required)) { Invoke-Rollback ('projection is missing ' + $required + ' after apply') }
}
if ($projectionAfter -match 'DbContext') { Invoke-Rollback 'the Application projection references a DbContext; model knowledge belongs to Infrastructure' }
Write-Ok 'projection carries the pure decision only'

function Remove-Comments {
    param([string] $Text)
    $noBlock = [regex]::Replace($Text, '/\*.*?\*/', '', 'Singleline')
    return [regex]::Replace($noBlock, '(?m)^\s*//.*$', '')
}

$scanTargets = @($ProjectionPath, $RefusalPath, $ResolverIfPath, $ResolverPath, $TestPath)
$forbidden = @('ProductFamily', 'GradeOrRecipe', 'ShiftCode', 'DefectType', 'RiskClass', 'productFamily', 'gradeOrRecipe', 'shiftCode', 'defectType', 'riskClass')
foreach ($target in $scanTargets) {
    $stripped = Remove-Comments -Text (Read-SourceText -Path $target)
    foreach ($term in $forbidden) {
        if ($stripped -cmatch [regex]::Escape($term)) {
            Invoke-Rollback ('plant vocabulary "' + $term + '" appears in ' + (Split-Path $target -Leaf))
        }
    }
}
Write-Ok 'no plant vocabulary in the files this pack wrote'

$serviceAfter = Read-SourceText -Path $ServicePath
foreach ($required in @('relatedDeclaredFilters', 'query.ElementType', '_subjectLinkResolver')) {
    if ($serviceAfter -notmatch [regex]::Escape($required)) { Invoke-Rollback ('the service is missing ' + $required) }
}
Write-Ok 'service routes declared filters by binding entity'

# Stage 1 is additive. The three compiled slots must still be standing, or this
# pack has quietly become stage 2 without the frontend.
foreach ($slot in @('filters?.ShiftCode', 'filters?.RiskClass', 'filters?.DefectType')) {
    if ($serviceAfter -notmatch [regex]::Escape($slot)) {
        Invoke-Rollback ('stage 1 must be additive, but ' + $slot + ' is gone; the cut belongs to stage 2 with the frontend')
    }
}
Write-Ok 'additive: the compiled slots are untouched and the wire contract is unchanged'

# ===========================================================================
# PHASE 6 - BUILD
# ===========================================================================
Write-Step 'Phase 6 - build'

$buildLog = Join-Path $LogDir 'build.log'
$buildExit = Invoke-NativeLogged 'dotnet' @('build', (Join-Path $RepoRoot 'Backend\PlantProcessIQ.sln'), '-v', 'm', '--nologo', '--no-incremental') $buildLog

if (-not (Test-Path -LiteralPath $buildLog)) { Invoke-Rollback 'the build produced no log; a missing log is not a clean build' }
if ((Get-Item -LiteralPath $buildLog).Length -eq 0) { Invoke-Rollback 'the build log is empty; zero error lines in an empty file is not evidence' }
if ($buildExit -ne 0) {
    $errors = @(Get-Content -LiteralPath $buildLog | Where-Object { $_ -cmatch '(error\s+[A-Z]{2,4}\d+)' } | Select-Object -First 20)
    foreach ($line in $errors) { Write-Bad $line }
    Invoke-Rollback ('build failed with exit ' + $buildExit)
}
Write-Ok 'build GREEN'

# ===========================================================================
# PHASE 7 - GATES
# ===========================================================================
Write-Step 'Phase 7 - gates'

$bindingAfter = Invoke-TestFilter -Filter 'FullyQualifiedName~DeclaredDimensionBindingTests' -LogName 'binding-after.log'
if ($bindingAfter.Failed -ne 0 -or $bindingAfter.Total -ne $bindingBefore.Total) {
    Invoke-Rollback ('the part 1 binding suite changed: ' + $bindingBefore.Passed + '/' + $bindingBefore.Total + ' -> ' + $bindingAfter.Passed + '/' + $bindingAfter.Total)
}
Write-Ok ('part 1 binding suite unchanged and GREEN : ' + $bindingAfter.Passed + '/' + $bindingAfter.Total)

$subjectLink = Invoke-TestFilter -Filter 'FullyQualifiedName~DeclaredDimensionSubjectLinkTests' -LogName 'subjectlink.log'
if ($subjectLink.Failed -ne 0) { Invoke-Rollback ('the stage 1 suite failed: ' + $subjectLink.Failed + ' failing') }
if ($subjectLink.Total -lt 10) { Invoke-Rollback ('the stage 1 suite ran ' + $subjectLink.Total + ' tests; a filter that matches nothing is not a pass') }
Write-Ok ('stage 1 suite GREEN : ' + $subjectLink.Passed + '/' + $subjectLink.Total)

$ua08After = Invoke-TestFilter -Filter 'FullyQualifiedName~Genericity' -LogName 'genericity-after.log'
if ($ua08After.Failed -ne 0) { Invoke-Rollback ('the genericity gate went red: ' + $ua08After.Failed + ' failing; stage 1 adds no fingerprint') }
if ($ua08After.Total -ne $ua08Before.Total) {
    Invoke-Rollback ('the genericity gate test count moved ' + $ua08Before.Total + ' -> ' + $ua08After.Total)
}
Write-Ok ('genericity gate GREEN and unchanged : ' + $ua08After.Passed + '/' + $ua08After.Total)

$adjacent = Invoke-TestFilter -Filter 'FullyQualifiedName~Dashboard|FullyQualifiedName~Widget|FullyQualifiedName~Dimension' -LogName 'adjacent.log'
if ($adjacent.Failed -ne 0) { Invoke-Rollback ('adjacent dashboard/widget/dimension tests failed: ' + $adjacent.Failed) }
Write-Ok ('adjacent suites GREEN : ' + $adjacent.Passed + '/' + $adjacent.Total)

$jobLogAfter = Invoke-TestFilter -Filter 'FullyQualifiedName~JobLogObservabilitySourceGuardTests' -LogName 'joblog-after.log'
if ($jobLogAfter.Failed -ne $jobLogBefore.Failed -or $jobLogAfter.Total -ne $jobLogBefore.Total) {
    Invoke-Rollback ('the JobLog identity moved: ' + $jobLogBefore.Failed + '/' + $jobLogBefore.Total + ' -> ' + $jobLogAfter.Failed + '/' + $jobLogAfter.Total)
}
Write-Ok ('JobLog : INHERITED, identical before and after (' + $jobLogAfter.Failed + ' failing of ' + $jobLogAfter.Total + ')')

# ===========================================================================
# PHASE 8 - STAGE, REPAIR, VERIFY
# ===========================================================================
Write-Step 'Phase 8 - exact staging'

Push-Location $RepoRoot
try {
    foreach ($relative in $Script:OwnedPaths) {
        [void](Invoke-NativeLines 'git' @('add', '--', $relative))
        if ($Script:LastNativeExit -ne 0) { Pop-Location; Invoke-Rollback ('git add failed for ' + $relative) }
    }

    $index = @(Invoke-NativeLines 'git' @('diff', '--cached', '--name-only') | ForEach-Object { $_.TrimEnd("`r") })
    $unexpected = @($index | Where-Object { $Script:OwnedPaths -notcontains $_ })
    if ($unexpected.Count -gt 0) {
        Pop-Location
        Invoke-Rollback ('the index carries paths this pack does not own: ' + ($unexpected -join '; '))
    }
    if ($index.Count -ne $Script:OwnedPaths.Count) {
        Pop-Location
        Invoke-Rollback ('the index holds ' + $index.Count + ' path(s); the manifest holds ' + $Script:OwnedPaths.Count)
    }
    foreach ($relative in $index) { Write-Host ('         ' + $relative) }

    # W1-PACK-CRLF-02. A line git names may carry no space at all and a bare CR:
    # the file is stored with LF and these lines carry an extra one. Removing the
    # whitespace alone leaves the CR, and git still calls it trailing whitespace.
    for ($pass = 1; $pass -le 3; $pass++) {
        $check = @(Invoke-NativeLines 'git' @('diff', '--cached', '--check'))
        if ($Script:LastNativeExit -eq 0) { break }

        $repaired = 0
        foreach ($line in $check) {
            if ($line -notmatch '^(.+?):(\d+):\s+trailing whitespace') { continue }
            $path = $Matches[1]
            $number = [int]$Matches[2]
            if ($Script:OwnedPaths -notcontains $path) { continue }

            $full = Join-Path $RepoRoot ($path -replace '/', '\')
            $text = Read-SourceText -Path $full
            $lines = $text -split "`n"
            if ($number -gt $lines.Count) { continue }

            $original = $lines[$number - 1]
            $fixed = $original -replace '[ \t]+$', ''
            $fixed = $fixed -replace "`r$", ''
            if ($fixed -ne $original) {
                $lines[$number - 1] = $fixed
                Write-SourceText -Path $full -Text ($lines -join "`n")
                Write-Ok ('repaired ' + $path + ' line ' + $number)
                $repaired++
            }
        }

        if ($repaired -eq 0) {
            foreach ($line in $check) { Write-Bad $line }
            Pop-Location
            Invoke-Rollback 'git diff --cached --check reported whitespace this pack could not attribute to an owned line'
        }

        foreach ($relative in $Script:OwnedPaths) { [void](Invoke-NativeLines 'git' @('add', '--', $relative)) }
    }

    [void](Invoke-NativeLines 'git' @('diff', '--cached', '--check'))
    if ($Script:LastNativeExit -ne 0) {
        Pop-Location
        Invoke-Rollback 'the staged view is still not clean after three repair passes'
    }
    Write-Ok 'index == owned manifest; --check GREEN'

    $allDirtyNow = @(Invoke-NativeLines 'git' @('status', '--porcelain', '--untracked-files=all'))
    $foreignNow = @($allDirtyNow | Where-Object {
        $line = $_.TrimEnd("`r")
        if ($line.Length -lt 4) { return $false }
        $path = $line.Substring(3).Trim().Trim('"')
        return -not ($Script:OwnedPaths -contains $path)
    })
    if ($foreignNow.Count -ne $Script:ForeignBefore) {
        Pop-Location
        Invoke-Rollback ('foreign worktree entries moved ' + $Script:ForeignBefore + ' -> ' + $foreignNow.Count + '; another lane wrote during this window')
    }
    Write-Ok ('foreign worktree preserved : ' + $foreignNow.Count + ' entries')
} finally {
    if ((Get-Location).Path -eq $RepoRoot) { Pop-Location }
}

Write-Step 'Phase 9 - re-verify after staging'

$reverify = Invoke-TestFilter -Filter 'FullyQualifiedName~DeclaredDimension' -LogName 'reverify.log'
if ($reverify.Failed -ne 0) { Invoke-Rollback ('re-verification failed after staging: ' + $reverify.Failed + ' failing') }
Write-Ok ('declared-dimension suites still GREEN : ' + $reverify.Passed + '/' + $reverify.Total)

# ===========================================================================
# PHASE 10 - RESULT
# ===========================================================================
if ($Mode -eq 'Validate') {
    Write-Step 'Phase 10 - VALIDATE mode: restoring the tree'

    Push-Location $RepoRoot
    foreach ($relative in $Script:OwnedPaths) { [void](Invoke-NativeLines 'git' @('reset', '--quiet', 'HEAD', '--', $relative)) }
    Pop-Location

    foreach ($relative in $Script:OwnedPaths) {
        $full   = Join-Path $RepoRoot ($relative -replace '/', '\')
        $backup = Join-Path $BackupDir (($relative -replace '[\\/]', '__'))
        if (Test-Path -LiteralPath $backup) {
            Copy-Item -LiteralPath $backup -Destination $full -Force
            (Get-Item -LiteralPath $full).LastWriteTime = Get-Date
        } elseif (Test-Path -LiteralPath $full) {
            Remove-Item -LiteralPath $full -Force
        }
    }
    Write-Ok 'owned paths restored; the repository is exactly as found'
}

Write-Host ''
Write-Host '=== RESULT ==='
Write-Host 'T-094 part 2b stage 1 - declared dimensions bind across the subject link'
Write-Host ''
Write-Host ('mode                         : ' + $Mode)
Write-Host ('parent SHA                   : ' + $head[0])
Write-Host ('branch                       : ' + $branch[0])
Write-Host  'anchored replacements        : 9, each matched exactly once'
Write-Host  'created files                : 3'
Write-Host ('build                        : GREEN, exit ' + $buildExit)
Write-Host ('part 1 binding suite         : ' + $bindingAfter.Passed + '/' + $bindingAfter.Total + ' (unchanged)')
Write-Host ('stage 1 suite                : ' + $subjectLink.Passed + '/' + $subjectLink.Total)
Write-Host ('genericity gate              : ' + $ua08After.Passed + '/' + $ua08After.Total + ' GREEN, no new fingerprint')
Write-Host ('adjacent dashboard/widget    : ' + $adjacent.Passed + '/' + $adjacent.Total)
Write-Host ('JobLog                       : INHERITED, ' + $jobLogAfter.Failed + ' failing of ' + $jobLogAfter.Total + ', identical before and after')
Write-Host ('foreign worktree             : ' + $Script:ForeignBefore + ' entries, preserved')
Write-Host ('evidence                     : ' + $LogDir)
Write-Host ('backups                      : ' + $BackupDir)
Write-Host ''
Write-Host 'ADDITIVE. No slot removed, no wire contract changed, no fingerprint retired.'
Write-Host 'The cut lands in stage 2 together with the frontend, as one valid state.'

if ($Mode -eq 'Commit') {
    Write-Step 'Phase 11 - commit'

    Push-Location $RepoRoot
    try {
        $message = 'T-094 bind declared dimensions across the subject link'
        [void](Invoke-NativeLines 'git' @('commit', '-m', $message))
        if ($Script:LastNativeExit -ne 0) {
            Pop-Location
            Write-Bad 'commit failed'
            exit 4
        }

        $newHead = @(Invoke-NativeLines 'git' @('rev-parse', 'HEAD'))
        Write-Host ''
        Write-Host ('commit SHA                   : ' + $newHead[0])
        Write-Host ('commit subject               : ' + $message)
    } finally {
        if ((Get-Location).Path -eq $RepoRoot) { Pop-Location }
    }
}

exit 0
