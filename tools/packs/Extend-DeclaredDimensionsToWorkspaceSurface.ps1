# Extend-DeclaredDimensionsToWorkspaceSurface.ps1   (stage 2A, r1)
#
# T-094 part 2b, stage 2A: the second wire surface gains the keyed declared-
# dimension contract. Owner Worker 1. Release M2-P1. Design 4.5.13b (v4.10.2).
#
# WHAT IT DOES. DashboardQueryDto - the workspace/global filter contract behind
# /overview, /quality, /risk, /data-quality, /materials and POST /workspace -
# gains DimensionFilters, keyed by published code, and DashboardQueryService
# executes them through the same declared-binding contract the widget surface
# uses since 377aabdf and e74ecd1c: a declaration on the subject entity restricts
# the population directly; one on a related entity is reached through the single
# mapped reference to the subject and intersected. The five GET routes accept a
# repeatable `dimensionFilter=code:value` query parameter. The reference-data
# route publishes the tenant's declared dimensions so a consumer can render what
# the registry says instead of what the product compiled. A refusal anywhere on
# the group surfaces as the same typed BusinessRule failure the widget surface
# returns.
#
# ADDITIVE. The legacy named parameters and the three compiled slots are NOT
# removed here; every existing caller behaves exactly as before. The cut is
# stage 2B, together with the frontend, as one valid state. This pack refuses
# itself if a compiled slot has gone missing.
#
# Encoding: two of the three edited files carry a UTF-8 BOM on disk. The BOM is
# preserved byte-for-byte; this task does not touch encoding.
#
# MODE. Default Validate: apply, gate, restore. -Mode Commit keeps and commits.
# Exit: 0 green; 2 refused before mutation; 3 gate failed, restored; 4 commit failed.

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
$BackupDir = Join-Path $BackupRoot ('t094b2a_' + $Stamp)
$LogDir    = Join-Path $EvidenceDir ('t094b2a_' + $Stamp)

function Write-Step { param([string] $Text) Write-Host ''; Write-Host ('=== ' + $Text + ' ===') }
function Write-Ok   { param([string] $Text) Write-Host ('  [ok]   ' + $Text) }
function Write-Warn { param([string] $Text) Write-Host ('  [warn] ' + $Text) }
function Write-Bad  { param([string] $Text) Write-Host ('  [BAD]  ' + $Text) }

function Stop-Refused {
    param([string] $Reason)
    Write-Bad ('REFUSED BEFORE MUTATION: ' + $Reason)
    exit 2
}

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
    try { return -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) } finally { $sha.Dispose() }
}

# BOM-aware. A file that carried a BOM is written back with it; one that did not
# is written without. Changing encoding is not this task's work.
function Read-SourceFile {
    param([string] $Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $text = (New-Object System.Text.UTF8Encoding($false)).GetString($bytes)
    if ($hasBom -and $text.Length -gt 0 -and [int]$text[0] -eq 0xFEFF) { $text = $text.Substring(1) }
    return @{ Text = $text; Bom = $hasBom }
}

function Write-SourceFile {
    param([string] $Path, [string] $Text, [bool] $Bom)
    $encoding = New-Object System.Text.UTF8Encoding($Bom)
    [System.IO.File]::WriteAllText($Path, $Text, $encoding)
}

# ---------------------------------------------------------------------------
$Script:Applied = $false

function Invoke-Rollback {
    param([string] $Reason)
    Write-Bad ('FAILURE: ' + $Reason)
    if (-not $Script:Applied) { Write-Ok 'nothing was applied; the tree was never mutated'; exit 3 }

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
        foreach ($relative in $Script:OwnedPaths) { [void](Invoke-NativeLines 'git' @('reset', '--quiet', 'HEAD', '--', $relative)) }
        $dirtyArgs = @('status', '--porcelain', '--untracked-files=all', '--') + $Script:OwnedPaths
        $dirty = @(Invoke-NativeLines 'git' $dirtyArgs)
        Write-Ok ('owned paths dirty after rollback : ' + $dirty.Count)
        $head = @(Invoke-NativeLines 'git' @('rev-parse', 'HEAD'))
        Write-Ok ('HEAD after rollback              : ' + $head[0])
    } finally { Pop-Location }
    exit 3
}

trap {
    if ($Script:Applied) { Invoke-Rollback ('unhandled error: ' + $_.Exception.Message) }
    Write-Bad ('unhandled error before mutation: ' + $_.Exception.Message)
    exit 2
}

# ---------------------------------------------------------------------------
$Script:OwnedPaths = @(
    'Backend/PlantProcess.Application/Dashboarding/Contracts/DashboardDtos.cs'
    'Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DimensionBindingRefusalException.cs'
    'Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DeclaredDimensionFilterQueryParser.cs'
    'Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardQueryService.cs'
    'Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs'
    'Backend/tests/PlantProcess.Architecture.Tests/WorkspaceDeclaredDimensionContractTests.cs'
)
$Script:CreatedPaths = @(
    'Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DeclaredDimensionFilterQueryParser.cs'
    'Backend/tests/PlantProcess.Architecture.Tests/WorkspaceDeclaredDimensionContractTests.cs'
)

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
New-Item -ItemType Directory -Force -Path $LogDir    | Out-Null

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
if ($staged.Count -gt 0) { Pop-Location; Stop-Refused ('the index is not empty (' + $staged.Count + ' path(s))') }
Write-Ok 'index is empty'

[void](Invoke-NativeLines 'git' @('merge-base', '--is-ancestor', 'e74ecd1c7383c8171dc42a864aab658f195441e7', 'HEAD'))
if ($Script:LastNativeExit -ne 0) { Pop-Location; Stop-Refused 'stage 1 (e74ecd1c) is not an ancestor of HEAD' }
[void](Invoke-NativeLines 'git' @('merge-base', '--is-ancestor', '4d32c6b10b4605aeb6e0705ad03537d563125652', 'HEAD'))
if ($Script:LastNativeExit -ne 0) { Pop-Location; Stop-Refused 'the frozen T-244 commit is not an ancestor of HEAD' }
Write-Ok 'stage 1 and T-244 are ancestors of HEAD'

$allDirty = @(Invoke-NativeLines 'git' @('status', '--porcelain', '--untracked-files=all'))
$foreign = @($allDirty | Where-Object {
    $line = $_.TrimEnd("`r"); if ($line.Length -lt 4) { return $false }
    $path = $line.Substring(3).Trim().Trim('"'); return -not ($Script:OwnedPaths -contains $path)
})
$Script:ForeignBefore = $foreign.Count
$Script:ForeignFingerprint = (Get-TextSha256 -Text (($foreign | Sort-Object) -join "`n")).Substring(0, 16).ToUpper()
Write-Ok ('foreign fingerprint : ' + $Script:ForeignFingerprint + ' (' + $Script:ForeignBefore + ' entries)')
Pop-Location

# ===========================================================================
Write-Step 'Phase 1 - preconditions'

$DtosPath     = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Contracts\DashboardDtos.cs'
$RefusalPath  = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Services\Dimensions\DimensionBindingRefusalException.cs'
$ParserPath   = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Services\Dimensions\DeclaredDimensionFilterQueryParser.cs'
$ServicePath  = Join-Path $RepoRoot 'Backend\PlantProcess.Application\Dashboarding\Services\Queries\DashboardQueryService.cs'
$EndpointPath = Join-Path $RepoRoot 'Backend\PlantProcess.Api\Endpoints\Dashboarding\DashboardEndpoints.cs'
$TestPath     = Join-Path $RepoRoot 'Backend\tests\PlantProcess.Architecture.Tests\WorkspaceDeclaredDimensionContractTests.cs'

foreach ($p in @($DtosPath, $RefusalPath, $ServicePath, $EndpointPath)) { if (-not (Test-Path -LiteralPath $p)) { Stop-Refused ('required source not found: ' + $p) } }
foreach ($p in @($ParserPath, $TestPath)) { if (Test-Path -LiteralPath $p) { Stop-Refused ('a file this pack creates already exists: ' + $p) } }

$refusalText = (Read-SourceFile -Path $RefusalPath).Text
$serviceText = (Read-SourceFile -Path $ServicePath).Text
$dtoText     = (Read-SourceFile -Path $DtosPath).Text

if ($refusalText -notmatch 'SubjectLinkUnavailable') { Stop-Refused 'stage 1 refusal codes are not present; apply Bind-DeclaredDimensionsAcrossSubjectLinks first' }
if ($serviceText -match 'DimensionFilters') { Stop-Refused 'stage 2A appears to be applied already (DashboardQueryService already carries DimensionFilters)' }
if ($dtoText -match 'DashboardReferenceDeclaredDimensionDto') { Stop-Refused 'stage 2A appears to be applied already (reference DTO present)' }
Write-Ok 'stage 1 present; stage 2A not yet applied'

$existingOwned = @(
    'Backend/PlantProcess.Application/Dashboarding/Contracts/DashboardDtos.cs'
    'Backend/PlantProcess.Application/Dashboarding/Services/Dimensions/DimensionBindingRefusalException.cs'
    'Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardQueryService.cs'
    'Backend/PlantProcess.Api/Endpoints/Dashboarding/DashboardEndpoints.cs'
)
Push-Location $RepoRoot
$ownedStatusArgs = @('status', '--porcelain', '--untracked-files=all', '--') + $existingOwned
$ownedDirty = @(Invoke-NativeLines 'git' $ownedStatusArgs)
Pop-Location
if ($ownedDirty.Count -gt 0) { Stop-Refused ('owned paths are not clean: ' + ($ownedDirty -join '; ')) }
Write-Ok 'owned paths are clean'

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
Write-Ok ('JobLog pristine : failing ' + $jobLogBefore.Failed + ' of ' + $jobLogBefore.Total)

$declaredBefore = Invoke-TestFilter -Filter 'FullyQualifiedName~DeclaredDimension' -LogName 'declared-before.log'
if ($declaredBefore.Failed -ne 0 -or $declaredBefore.Total -lt 18) { Stop-Refused ('declared-dimension suites are not green before this pack: ' + $declaredBefore.Passed + '/' + $declaredBefore.Total) }
Write-Ok ('declared-dimension suites pristine : ' + $declaredBefore.Passed + '/' + $declaredBefore.Total)

$ua08Before = Invoke-TestFilter -Filter 'FullyQualifiedName~Genericity' -LogName 'genericity-before.log'
if ($ua08Before.Failed -ne 0) { Stop-Refused 'the genericity gate is not green before this pack' }
Write-Ok ('genericity gate pristine : ' + $ua08Before.Passed + '/' + $ua08Before.Total)

# ===========================================================================
Write-Step 'Phase 3 - exact owned backups'
foreach ($relative in $Script:OwnedPaths) {
    if ($Script:CreatedPaths -contains $relative) { continue }
    $full = Join-Path $RepoRoot ($relative -replace '/', '\')
    Copy-Item -LiteralPath $full -Destination (Join-Path $BackupDir (($relative -replace '[\\/]', '__'))) -Force
    Write-Ok ('backed up ' + $relative)
}

# ===========================================================================
Write-Step 'Phase 4 - apply'
$Script:Applied = $true

function Invoke-AnchoredEdit {
    param([string] $Path, [string] $Label, [string] $Old, [string] $New)
    $file = Read-SourceFile -Path $Path
    $crlf = $file.Text.Contains("`r`n")
    $flat = $file.Text -replace "`r`n", "`n"
    $oldFlat = $Old -replace "`r`n", "`n"
    $newFlat = $New -replace "`r`n", "`n"
    $first = $flat.IndexOf($oldFlat, [System.StringComparison]::Ordinal)
    if ($first -lt 0) { Invoke-Rollback ('anchor not found: ' + $Label) }
    if ($flat.IndexOf($oldFlat, $first + 1, [System.StringComparison]::Ordinal) -ge 0) { Invoke-Rollback ('anchor matched more than once: ' + $Label) }
    $flat = $flat.Substring(0, $first) + $newFlat + $flat.Substring($first + $oldFlat.Length)
    if ($crlf) { $flat = $flat -replace "`n", "`r`n" }
    Write-SourceFile -Path $Path -Text $flat -Bom $file.Bom
    Write-Ok ('applied: ' + $Label)
}

function New-SourceFile {
    param([string] $Path, [string] $Label, [string] $Body)
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    $text = ($Body -replace "`r`n", "`n") -replace "`n", "`r`n"
    Write-SourceFile -Path $Path -Text $text -Bom $false
    Write-Ok ('created: ' + $Label)
}

# --- 1. contract: the workspace query and the reference data gain the keyed shape
$dtoQueryOld = @'
    string? SortBy,
    string? SortDirection)
{
    public int SafePage => Page <= 0 ? 1 : Page;
'@
$dtoQueryNew = @'
    string? SortBy,
    string? SortDirection,
    IReadOnlyList<DeclaredDimensionFilterDto>? DimensionFilters = null)
{
    public int SafePage => Page <= 0 ? 1 : Page;
'@
Invoke-AnchoredEdit -Path $DtosPath -Label 'contract: workspace query carries keyed declared filters' -Old $dtoQueryOld -New $dtoQueryNew

$dtoRefOld = @'
    IReadOnlyList<DashboardReferenceItemDto> RiskClasses,
    IReadOnlyList<DashboardReferenceItemDto> Shifts);
'@
$dtoRefNew = @'
    IReadOnlyList<DashboardReferenceItemDto> RiskClasses,
    IReadOnlyList<DashboardReferenceItemDto> Shifts,
    IReadOnlyList<DashboardReferenceDeclaredDimensionDto>? DeclaredDimensions = null);

/// <summary>
/// T-094. A dimension the tenant has PUBLISHED, offered to a consumer so it can
/// render what the registry declares rather than what the product compiled.
/// Values are not enumerated here; a consumer enumerates them through the
/// widget-query surface, which already groups by any declared code.
/// </summary>
public sealed record DashboardReferenceDeclaredDimensionDto(
    string Code,
    string Label,
    string DataType,
    string GrainCode,
    bool IsExecutable,
    string? RefusalCode);
'@
Invoke-AnchoredEdit -Path $DtosPath -Label 'contract: reference data publishes declared dimensions' -Old $dtoRefOld -New $dtoRefNew

# --- 2. one more typed refusal: a malformed keyed filter on the query string
$refusalOld = @'
    public const string SubjectLinkUnavailable = "DB08_subject_link_unavailable";
'@
$refusalNew = @'
    public const string SubjectLinkUnavailable = "DB08_subject_link_unavailable";

    /// <summary>
    /// A keyed filter arrived on the wire in a shape that names no code or no
    /// value. It is refused as itself, never silently dropped: a dropped filter
    /// widens the population and reports the wider number as the answer.
    /// </summary>
    public const string FilterMalformed = "DB09_dimension_filter_malformed";
'@
Invoke-AnchoredEdit -Path $RefusalPath -Label 'refusal: malformed keyed filter' -Old $refusalOld -New $refusalNew

# --- 3. pure parser for the query-string form
$parserSource = @'
using System;
using System.Collections.Generic;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Widgets;

namespace PlantProcess.Application.Dashboarding.Services.Dimensions;

/// <summary>
/// The query-string form of a keyed declared-dimension filter: one repeatable
/// parameter, each occurrence "code:value". The first separator splits; a value
/// may itself contain the separator. Codes are validated for shape only - whether
/// a code is DECLARED is the catalogue's question, answered at execution as a
/// typed refusal, never here and never by a compiled list.
/// </summary>
public static class DeclaredDimensionFilterQueryParser
{
    public const string ParameterName = "dimensionFilter";
    public const char Separator = ':';

    public static IReadOnlyList<DeclaredDimensionFilterDto>? Parse(IEnumerable<string>? raw)
    {
        if (raw is null) { return null; }

        var parsed = new List<DeclaredDimensionFilterDto>();

        foreach (var entry in raw)
        {
            if (string.IsNullOrWhiteSpace(entry)) { continue; }

            var separator = entry.IndexOf(Separator);
            if (separator <= 0 || separator == entry.Length - 1)
            {
                throw new DimensionBindingRefusalException(
                    DimensionBindingRefusalCodes.FilterMalformed,
                    entry.Trim(),
                    "Declared-dimension filter '" + entry.Trim() + "' must be written as code" + Separator + "value.");
            }

            var code = entry.Substring(0, separator).Trim();
            var value = entry.Substring(separator + 1).Trim();

            if (!DashboardWidgetQuerySafetyRegistry.IsWellFormedDeclaredCode(code))
            {
                throw new DimensionBindingRefusalException(
                    DimensionBindingRefusalCodes.FilterMalformed,
                    code,
                    "Declared-dimension filter code '" + code + "' is not a well-formed dimension code.");
            }

            if (value.Length == 0)
            {
                throw new DimensionBindingRefusalException(
                    DimensionBindingRefusalCodes.FilterMalformed,
                    code,
                    "Declared-dimension filter '" + code + "' carries no value.");
            }

            parsed.Add(new DeclaredDimensionFilterDto(code, value));
        }

        return parsed.Count == 0 ? null : parsed;
    }

    /// <summary>
    /// Trim and drop empty entries from an already-typed list; a POSTed body may
    /// carry blanks that a query string would never produce.
    /// </summary>
    public static IReadOnlyList<DeclaredDimensionFilterDto>? Normalise(IReadOnlyList<DeclaredDimensionFilterDto>? filters)
    {
        if (filters is null || filters.Count == 0) { return null; }

        var kept = new List<DeclaredDimensionFilterDto>(filters.Count);
        foreach (var filter in filters)
        {
            if (filter is null || string.IsNullOrWhiteSpace(filter.Code)) { continue; }
            kept.Add(new DeclaredDimensionFilterDto(filter.Code.Trim(), (filter.Value ?? string.Empty).Trim()));
        }

        return kept.Count == 0 ? null : kept;
    }
}
'@
New-SourceFile -Path $ParserPath -Label 'DeclaredDimensionFilterQueryParser.cs' -Body $parserSource

# --- 4. the workspace service executes keyed filters through the declared contract
$svcUsingsOld = @'
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Dashboarding.Interfaces;
'@
$svcUsingsNew = @'
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Dashboarding.Interfaces;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using PlantProcess.Application.Security.Tenancy;
'@
Invoke-AnchoredEdit -Path $ServicePath -Label 'service: usings' -Old $svcUsingsOld -New $svcUsingsNew

$svcCtorOld = @'
    private readonly IPlantProcessDbContext _dbContext;

    public DashboardQueryService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }
'@
$svcCtorNew = @'
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IDeclaredDimensionCatalog? _declaredDimensions;
    private readonly IDeclaredDimensionSubjectLinkResolver? _subjectLinkResolver;
    private readonly ITenantAccessor? _tenantAccessor;

    public DashboardQueryService(
        IPlantProcessDbContext dbContext,
        IDeclaredDimensionCatalog? declaredDimensions = null,
        IDeclaredDimensionSubjectLinkResolver? subjectLinkResolver = null,
        ITenantAccessor? tenantAccessor = null)
    {
        _dbContext = dbContext;
        _declaredDimensions = declaredDimensions;
        _subjectLinkResolver = subjectLinkResolver;
        _tenantAccessor = tenantAccessor;
    }
'@
Invoke-AnchoredEdit -Path $ServicePath -Label 'service: optional declared-dimension dependencies' -Old $svcCtorOld -New $svcCtorNew

$svcSubjectOld = @'
        if (!string.IsNullOrWhiteSpace(normalized.SourceSystem))
            materialsQuery = materialsQuery.Where(x => x.SourceSystem == normalized.SourceSystem);

        var materialIds = await materialsQuery
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var materialSet = materialIds.ToHashSet();
'@
$svcSubjectNew = @'
        if (!string.IsNullOrWhiteSpace(normalized.SourceSystem))
            materialsQuery = materialsQuery.Where(x => x.SourceSystem == normalized.SourceSystem);

        // T-094. Keyed declared-dimension filters on the workspace surface, through the
        // same contract the widget surface uses. A declaration on the subject entity
        // restricts this population directly; one on a related entity is reached through
        // the single mapped reference to the subject and intersected below. Refusals are
        // typed and surface unchanged; nothing here falls back to a compiled word.
        var subjectEntityType = materialsQuery.ElementType;
        var relatedDeclaredFilters = new List<(DeclaredDimension Declared, string Value)>();

        if (normalized.DimensionFilters is { Count: > 0 })
        {
            foreach (var dimensionFilter in normalized.DimensionFilters)
            {
                var declaredFilter = await RequireDeclaredDimensionAsync(dimensionFilter.Code, cancellationToken);

                if (DeclaredDimensionProjection.BindsToSubject(declaredFilter, subjectEntityType))
                    materialsQuery = DeclaredDimensionProjection.WhereDeclaredEquals(materialsQuery, declaredFilter, dimensionFilter.Value);
                else
                    relatedDeclaredFilters.Add((declaredFilter, dimensionFilter.Value));
            }
        }

        var materialIds = await materialsQuery
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var materialSet = materialIds.ToHashSet();

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
                relatedFilter.Declared, subjectEntityType, relatedFilter.Value, cancellationToken);

            materialSet.IntersectWith(linkedKeys);
        }
'@
Invoke-AnchoredEdit -Path $ServicePath -Label 'service: keyed filters restrict the workspace population' -Old $svcSubjectOld -New $svcSubjectNew

$svcNormOld = @'
            ShiftCode = NormalizeText(query.ShiftCode),
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            SortDirection = query.SafeSortDirection
        };
    }
'@
$svcNormNew = @'
            ShiftCode = NormalizeText(query.ShiftCode),
            DimensionFilters = DeclaredDimensionFilterQueryParser.Normalise(query.DimensionFilters),
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            SortDirection = query.SafeSortDirection
        };
    }

    private async Task<DeclaredDimension> RequireDeclaredDimensionAsync(string dimensionCode, CancellationToken cancellationToken)
    {
        if (_declaredDimensions is null || _tenantAccessor is null || !_tenantAccessor.TryGetTenantId(out var tenantId))
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.TenantUnresolved,
                dimensionCode,
                "Declared dimension '" + dimensionCode + "' cannot be resolved without a tenant-scoped declaration catalogue.");
        }

        var declared = await _declaredDimensions.FindAsync(tenantId, dimensionCode, cancellationToken);

        if (declared is null)
        {
            throw new DimensionBindingRefusalException(
                DimensionBindingRefusalCodes.Undeclared,
                dimensionCode,
                "Dimension '" + dimensionCode + "' is neither a structural dimension nor a published declaration for this tenant.");
        }

        return declared;
    }
'@
Invoke-AnchoredEdit -Path $ServicePath -Label 'service: normalise keyed filters and resolve declarations' -Old $svcNormOld -New $svcNormNew

# --- 5. the routes accept the keyed form and surface refusals as the widget surface does
$epUsingsOld = @'
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;

namespace PlantProcess.Api.Endpoints.Dashboarding;
'@
$epUsingsNew = @'
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using PlantProcess.Application.Security.Tenancy;
using System.Security.Claims;

namespace PlantProcess.Api.Endpoints.Dashboarding;
'@
Invoke-AnchoredEdit -Path $EndpointPath -Label 'endpoints: usings' -Old $epUsingsOld -New $epUsingsNew

$epGroupOld = @'
        var group = app.MapGroup("/analytics/dashboard")
            .WithTags("Dashboard");
'@
$epGroupNew = @'
        var group = app.MapGroup("/analytics/dashboard")
            .WithTags("Dashboard");

        // T-094. A declared-dimension refusal raised on the workspace surface becomes
        // the same typed BusinessRule failure the widget surface already returns, so a
        // consumer sees one refusal vocabulary whichever route it asked.
        group.AddEndpointFilter(async (invocation, next) =>
        {
            try
            {
                return await next(invocation);
            }
            catch (DimensionBindingRefusalException refusal)
            {
                return ApplicationResult<object>
                    .Failure(ApplicationError.BusinessRule(refusal.RefusalCode + ": " + refusal.Message))
                    .ToHttpResult(_ => Results.Ok());
            }
        });
'@
Invoke-AnchoredEdit -Path $EndpointPath -Label 'endpoints: one refusal vocabulary across the group' -Old $epGroupOld -New $epGroupNew

$epOverviewOld = @'
        string? shiftCode,
        [Microsoft.AspNetCore.Mvc.FromServices] IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetOverviewAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, 25, null, null),
'@
$epOverviewNew = @'
        string? shiftCode,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = DeclaredDimensionFilterQueryParser.ParameterName)] string[]? dimensionFilter,
        [Microsoft.AspNetCore.Mvc.FromServices] IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetOverviewAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, 25, null, null, dimensionFilter),
'@
Invoke-AnchoredEdit -Path $EndpointPath -Label 'endpoints: /overview accepts keyed filters' -Old $epOverviewOld -New $epOverviewNew

$epQualityOld = @'
        string? shiftCode,
        [Microsoft.AspNetCore.Mvc.FromServices] IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetQualityDashboardAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, 25, null, null),
'@
$epQualityNew = @'
        string? shiftCode,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = DeclaredDimensionFilterQueryParser.ParameterName)] string[]? dimensionFilter,
        [Microsoft.AspNetCore.Mvc.FromServices] IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetQualityDashboardAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, 25, null, null, dimensionFilter),
'@
Invoke-AnchoredEdit -Path $EndpointPath -Label 'endpoints: /quality accepts keyed filters' -Old $epQualityOld -New $epQualityNew

$epRiskOld = @'
        string? shiftCode,
        int? highRiskTake,
        [Microsoft.AspNetCore.Mvc.FromServices] IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetRiskDashboardAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, highRiskTake ?? 25, null, null),
'@
$epRiskNew = @'
        string? shiftCode,
        int? highRiskTake,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = DeclaredDimensionFilterQueryParser.ParameterName)] string[]? dimensionFilter,
        [Microsoft.AspNetCore.Mvc.FromServices] IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetRiskDashboardAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, highRiskTake ?? 25, null, null, dimensionFilter),
'@
Invoke-AnchoredEdit -Path $EndpointPath -Label 'endpoints: /risk accepts keyed filters' -Old $epRiskOld -New $epRiskNew

$epDqOld = @'
        string? shiftCode,
        [Microsoft.AspNetCore.Mvc.FromServices] IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetDataQualityDashboardAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, 25, null, null),
'@
$epDqNew = @'
        string? shiftCode,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = DeclaredDimensionFilterQueryParser.ParameterName)] string[]? dimensionFilter,
        [Microsoft.AspNetCore.Mvc.FromServices] IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetDataQualityDashboardAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, 25, null, null, dimensionFilter),
'@
Invoke-AnchoredEdit -Path $EndpointPath -Label 'endpoints: /data-quality accepts keyed filters' -Old $epDqOld -New $epDqNew

$epSearchOld = @'
        string? sortDirection,
        [Microsoft.AspNetCore.Mvc.FromServices] IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchMaterialsAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, page ?? 1, pageSize ?? 25, sortBy, sortDirection),
'@
$epSearchNew = @'
        string? sortDirection,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = DeclaredDimensionFilterQueryParser.ParameterName)] string[]? dimensionFilter,
        [Microsoft.AspNetCore.Mvc.FromServices] IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchMaterialsAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, page ?? 1, pageSize ?? 25, sortBy, sortDirection, dimensionFilter),
'@
Invoke-AnchoredEdit -Path $EndpointPath -Label 'endpoints: /materials accepts keyed filters' -Old $epSearchOld -New $epSearchNew

$epBuildOld = @'
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection)
    {
        return new DashboardQueryDto(
            siteId,
            areaId,
            equipmentId,
            materialCode,
            sourceSystem,
            defectType,
            riskClass,
            fromUtc,
            toUtc,
            shiftCode,
            page,
            pageSize,
            sortBy,
            sortDirection);
    }
'@
$epBuildNew = @'
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        string[]? dimensionFilter = null)
    {
        return new DashboardQueryDto(
            siteId,
            areaId,
            equipmentId,
            materialCode,
            sourceSystem,
            defectType,
            riskClass,
            fromUtc,
            toUtc,
            shiftCode,
            page,
            pageSize,
            sortBy,
            sortDirection,
            DeclaredDimensionFilterQueryParser.Parse(dimensionFilter));
    }
'@
Invoke-AnchoredEdit -Path $EndpointPath -Label 'endpoints: BuildQuery carries the keyed set' -Old $epBuildOld -New $epBuildNew

$epRefSigOld = @'
    private static async Task<IResult> GetReferenceDataAsync(
        Guid? siteId,
        IMemoryCache cache,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"dashboard-reference-data:{siteId?.ToString() ?? "all"}";
'@
$epRefSigNew = @'
    private static async Task<IResult> GetReferenceDataAsync(
        Guid? siteId,
        ClaimsPrincipal user,
        IMemoryCache cache,
        PlantProcessDbContext dbContext,
        [Microsoft.AspNetCore.Mvc.FromServices] IDeclaredDimensionCatalog declaredDimensions,
        CancellationToken cancellationToken)
    {
        // T-094. Declared dimensions are tenant-scoped, so the cache is too.
        var tenantResolved = TenantClaims.TryResolve(user, out var tenantId) && tenantId != Guid.Empty;
        var tenantKey = tenantResolved ? tenantId.ToString("D") : "none";
        var cacheKey = $"dashboard-reference-data:{siteId?.ToString() ?? "all"}:{tenantKey}";
'@
Invoke-AnchoredEdit -Path $EndpointPath -Label 'endpoints: reference data is tenant-scoped' -Old $epRefSigOld -New $epRefSigNew

$epRefBuildOld = @'
        var result = new DashboardReferenceDataDto(
            DateTime.UtcNow,
            sites,
            areas,
            equipmentItems,
            sourceSystems,
            defects,
            parameters,
            riskClasses,
            shifts);
'@
$epRefBuildNew = @'
        var declared = new List<DashboardReferenceDeclaredDimensionDto>();
        if (tenantResolved)
        {
            foreach (var dimension in await declaredDimensions.GetPublishedAsync(tenantId, cancellationToken))
            {
                declared.Add(new DashboardReferenceDeclaredDimensionDto(
                    dimension.Code,
                    dimension.Label,
                    dimension.DataType,
                    dimension.GrainCode,
                    dimension.IsBindable,
                    dimension.BindingRefusalCode));
            }
        }

        var result = new DashboardReferenceDataDto(
            DateTime.UtcNow,
            sites,
            areas,
            equipmentItems,
            sourceSystems,
            defects,
            parameters,
            riskClasses,
            shifts,
            declared);
'@
Invoke-AnchoredEdit -Path $EndpointPath -Label 'endpoints: reference data publishes declared dimensions' -Old $epRefBuildOld -New $epRefBuildNew

# --- 6. the proof
$testSource = @'
using System;
using System.Collections.Generic;
using System.IO;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Dimensions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-094 part 2b, stage 2A: the workspace/global surface carries the same keyed
/// declared-dimension contract as the widget surface (design 4.5.13b). Proved
/// without a database: the query-string form parses to the same DTO the widget
/// surface accepts, malformed input is a typed refusal rather than a dropped
/// filter, and the contract and service sources carry the keyed path.
/// </summary>
[Trait("BacklogTask", "T-094")]
[Trait("Gate", "WorkspaceDeclaredDimensionContract")]
public sealed class WorkspaceDeclaredDimensionContractTests
{
    [Fact]
    public void A_query_string_entry_parses_to_the_keyed_filter_dto()
    {
        var parsed = DeclaredDimensionFilterQueryParser.Parse(new[] { "anyCode:some value" });

        var single = Assert.Single(parsed!);
        Assert.Equal("anyCode", single.Code);
        Assert.Equal("some value", single.Value);
    }

    [Fact]
    public void The_first_separator_splits_so_a_value_may_carry_the_separator()
    {
        var parsed = DeclaredDimensionFilterQueryParser.Parse(new[] { "code:a:b" });
        Assert.Equal("a:b", Assert.Single(parsed!).Value);
    }

    [Fact]
    public void Blank_entries_are_ignored_and_an_all_blank_list_is_no_filter()
    {
        Assert.Null(DeclaredDimensionFilterQueryParser.Parse(new[] { "", "   " }));
        Assert.Null(DeclaredDimensionFilterQueryParser.Parse(null));
        Assert.Null(DeclaredDimensionFilterQueryParser.Parse(Array.Empty<string>()));
    }

    [Theory]
    [InlineData("noSeparator")]
    [InlineData(":valueOnly")]
    [InlineData("codeOnly:")]
    [InlineData("9startsWithDigit:x")]
    [InlineData("has space:x")]
    public void A_malformed_entry_is_a_typed_refusal_never_a_dropped_filter(string entry)
    {
        var refusal = Assert.Throws<DimensionBindingRefusalException>(() =>
            DeclaredDimensionFilterQueryParser.Parse(new[] { entry }));

        Assert.Equal(DimensionBindingRefusalCodes.FilterMalformed, refusal.RefusalCode);
    }

    [Fact]
    public void A_posted_body_is_normalised_the_same_way()
    {
        var normalised = DeclaredDimensionFilterQueryParser.Normalise(new List<DeclaredDimensionFilterDto>
        {
            new(" code ", " v "),
            new("", "dropped"),
        });

        var single = Assert.Single(normalised!);
        Assert.Equal("code", single.Code);
        Assert.Equal("v", single.Value);
        Assert.Null(DeclaredDimensionFilterQueryParser.Normalise(null));
        Assert.Null(DeclaredDimensionFilterQueryParser.Normalise(new List<DeclaredDimensionFilterDto>()));
    }

    [Fact]
    public void The_workspace_query_contract_carries_the_keyed_set()
    {
        var query = new DashboardQueryDto(
            null, null, null, null, null, null, null, null, null, null, 1, 25, null, null,
            new[] { new DeclaredDimensionFilterDto("code", "value") });

        Assert.Single(query.DimensionFilters!);

        // Existing fourteen-argument construction still compiles and carries no filters.
        var legacy = new DashboardQueryDto(null, null, null, null, null, null, null, null, null, null, 1, 25, null, null);
        Assert.Null(legacy.DimensionFilters);
    }

    [Fact]
    public void Both_surfaces_resolve_declarations_through_the_same_binding_contract()
    {
        var root = ScopeAwareGenericity.RepositoryRoot();
        var workspace = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Queries", "DashboardQueryService.cs"));
        var widget = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Queries", "DashboardWidgetQueryService.cs"));

        foreach (var member in new[] { "BindsToSubject", "WhereDeclaredEquals", "SubjectKeysWhereDeclaredEqualsAsync", "RequireDeclaredDimensionAsync" })
        {
            Assert.Contains(member, workspace, StringComparison.Ordinal);
            Assert.Contains(member, widget, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_workspace_route_accepts_the_keyed_parameter()
    {
        var root = ScopeAwareGenericity.RepositoryRoot();
        var endpoints = File.ReadAllText(Path.Combine(root, "Backend", "PlantProcess.Api", "Endpoints", "Dashboarding", "DashboardEndpoints.cs"));

        var occurrences = 0;
        var index = 0;
        while ((index = endpoints.IndexOf("DeclaredDimensionFilterQueryParser.ParameterName", index, StringComparison.Ordinal)) >= 0)
        {
            occurrences++;
            index += 1;
        }

        // /overview, /quality, /risk, /data-quality, /materials.
        Assert.Equal(5, occurrences);
        Assert.Contains("AddEndpointFilter", endpoints, StringComparison.Ordinal);
    }
}
'@
New-SourceFile -Path $TestPath -Label 'WorkspaceDeclaredDimensionContractTests.cs' -Body $testSource

# ===========================================================================
Write-Step 'Phase 5 - self-check on disk'

$serviceAfter = (Read-SourceFile -Path $ServicePath).Text
foreach ($required in @('relatedDeclaredFilters', 'materialsQuery.ElementType', 'RequireDeclaredDimensionAsync', 'DeclaredDimensionFilterQueryParser.Normalise')) {
    if ($serviceAfter -notmatch [regex]::Escape($required)) { Invoke-Rollback ('the workspace service is missing ' + $required) }
}
# Stage 2A is additive. The three compiled slots stay standing on BOTH surfaces.
foreach ($slot in @('normalized.ShiftCode', 'normalized.DefectType', 'normalized.RiskClass')) {
    if ($serviceAfter -notmatch [regex]::Escape($slot)) { Invoke-Rollback ('stage 2A must be additive, but ' + $slot + ' is gone from the workspace service') }
}
Write-Ok 'workspace service: keyed path present, compiled slots untouched'

$endpointAfter = (Read-SourceFile -Path $EndpointPath).Text
$paramCount = ([regex]::Matches($endpointAfter, [regex]::Escape('DeclaredDimensionFilterQueryParser.ParameterName'))).Count
if ($paramCount -ne 5) { Invoke-Rollback ('expected the keyed parameter on 5 routes, found ' + $paramCount) }
foreach ($legacy in @('string? defectType,', 'string? riskClass,', 'string? shiftCode,')) {
    if ($endpointAfter -notmatch [regex]::Escape($legacy)) { Invoke-Rollback ('stage 2A must keep the legacy named parameters; ' + $legacy + ' is gone') }
}
Write-Ok 'endpoints: 5 routes carry the keyed parameter, legacy parameters retained'

function Remove-Comments { param([string] $Text) $noBlock = [regex]::Replace($Text, '/\*.*?\*/', '', 'Singleline'); return [regex]::Replace($noBlock, '(?m)^\s*//.*$', '') }
$forbidden = @('ProductFamily', 'GradeOrRecipe', 'ShiftCode', 'DefectType', 'RiskClass', 'productFamily', 'gradeOrRecipe', 'shiftCode', 'defectType', 'riskClass')
foreach ($target in @($ParserPath, $TestPath, $RefusalPath)) {
    $stripped = Remove-Comments -Text (Read-SourceFile -Path $target).Text
    foreach ($term in $forbidden) { if ($stripped -cmatch [regex]::Escape($term)) { Invoke-Rollback ('plant vocabulary "' + $term + '" appears in ' + (Split-Path $target -Leaf)) } }
}
Write-Ok 'no plant vocabulary in the files this pack created'

foreach ($p in @($DtosPath, $ServicePath)) {
    $bytes = [System.IO.File]::ReadAllBytes($p)
    if (-not ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) { Invoke-Rollback ('BOM was not preserved on ' + (Split-Path $p -Leaf)) }
}
$epBytes = [System.IO.File]::ReadAllBytes($EndpointPath)
if ($epBytes[0] -eq 0xEF -and $epBytes[1] -eq 0xBB -and $epBytes[2] -eq 0xBF) { Invoke-Rollback 'a BOM was introduced on DashboardEndpoints.cs' }
Write-Ok 'encoding preserved: BOM kept where it was, none introduced'

# ===========================================================================
Write-Step 'Phase 6 - build'
$buildLog = Join-Path $LogDir 'build.log'
$buildExit = Invoke-NativeLogged 'dotnet' @('build', (Join-Path $RepoRoot 'Backend\PlantProcessIQ.sln'), '-v', 'm', '--nologo', '--no-incremental') $buildLog
if (-not (Test-Path -LiteralPath $buildLog)) { Invoke-Rollback 'the build produced no log' }
if ((Get-Item -LiteralPath $buildLog).Length -eq 0) { Invoke-Rollback 'the build log is empty; not evidence' }
if ($buildExit -ne 0) {
    foreach ($line in @(Get-Content -LiteralPath $buildLog | Where-Object { $_ -cmatch '(error\s+[A-Z]{2,4}\d+)' } | Select-Object -First 20)) { Write-Bad $line }
    Invoke-Rollback ('build failed with exit ' + $buildExit)
}
Write-Ok 'build GREEN'

# ===========================================================================
Write-Step 'Phase 7 - gates'

$declaredAfter = Invoke-TestFilter -Filter 'FullyQualifiedName~DeclaredDimension' -LogName 'declared-after.log'
if ($declaredAfter.Failed -ne 0 -or $declaredAfter.Total -lt $declaredBefore.Total) {
    Invoke-Rollback ('declared-dimension suites moved: ' + $declaredBefore.Passed + '/' + $declaredBefore.Total + ' -> ' + $declaredAfter.Passed + '/' + $declaredAfter.Total)
}
Write-Ok ('declared-dimension suites GREEN : ' + $declaredAfter.Passed + '/' + $declaredAfter.Total)

$workspace = Invoke-TestFilter -Filter 'FullyQualifiedName~WorkspaceDeclaredDimensionContractTests' -LogName 'workspace.log'
if ($workspace.Failed -ne 0) { Invoke-Rollback ('the stage 2A suite failed: ' + $workspace.Failed + ' failing') }
if ($workspace.Total -lt 12) { Invoke-Rollback ('the stage 2A suite ran ' + $workspace.Total + ' cases; a filter that matches nothing is not a pass') }
Write-Ok ('stage 2A suite GREEN : ' + $workspace.Passed + '/' + $workspace.Total)

$ua08After = Invoke-TestFilter -Filter 'FullyQualifiedName~Genericity' -LogName 'genericity-after.log'
if ($ua08After.Failed -ne 0) { Invoke-Rollback ('the genericity gate went red: ' + $ua08After.Failed + ' failing') }
if ($ua08After.Total -ne $ua08Before.Total) { Invoke-Rollback ('the genericity gate test count moved ' + $ua08Before.Total + ' -> ' + $ua08After.Total) }
Write-Ok ('genericity gate GREEN and unchanged : ' + $ua08After.Passed + '/' + $ua08After.Total)

$adjacent = Invoke-TestFilter -Filter 'FullyQualifiedName~Dashboard|FullyQualifiedName~Widget|FullyQualifiedName~Dimension' -LogName 'adjacent.log'
if ($adjacent.Failed -ne 0) { Invoke-Rollback ('adjacent dashboard/widget/dimension tests failed: ' + $adjacent.Failed) }
Write-Ok ('adjacent suites GREEN : ' + $adjacent.Passed + '/' + $adjacent.Total)

$jobLogAfter = Invoke-TestFilter -Filter 'FullyQualifiedName~JobLogObservabilitySourceGuardTests' -LogName 'joblog-after.log'
if ($jobLogAfter.Failed -ne $jobLogBefore.Failed -or $jobLogAfter.Total -ne $jobLogBefore.Total) { Invoke-Rollback 'the JobLog identity moved' }
Write-Ok ('JobLog : INHERITED, identical before and after (' + $jobLogAfter.Failed + ' failing of ' + $jobLogAfter.Total + ')')

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
    if ($unexpected.Count -gt 0) { Pop-Location; Invoke-Rollback ('the index carries paths this pack does not own: ' + ($unexpected -join '; ')) }
    if ($index.Count -ne $Script:OwnedPaths.Count) { Pop-Location; Invoke-Rollback ('the index holds ' + $index.Count + ' path(s); the manifest holds ' + $Script:OwnedPaths.Count) }
    foreach ($relative in $index) { Write-Host ('         ' + $relative) }

    for ($pass = 1; $pass -le 3; $pass++) {
        $check = @(Invoke-NativeLines 'git' @('diff', '--cached', '--check'))
        if ($Script:LastNativeExit -eq 0) { break }
        $repaired = 0
        foreach ($line in $check) {
            if ($line -notmatch '^(.+?):(\d+):\s+trailing whitespace') { continue }
            $path = $Matches[1]; $number = [int]$Matches[2]
            if ($Script:OwnedPaths -notcontains $path) { continue }
            $full = Join-Path $RepoRoot ($path -replace '/', '\')
            $file = Read-SourceFile -Path $full
            $lines = $file.Text -split "`n"
            if ($number -gt $lines.Count) { continue }
            $original = $lines[$number - 1]
            $fixed = ($original -replace '[ \t]+$', '') -replace "`r$", ''
            if ($fixed -ne $original) {
                $lines[$number - 1] = $fixed
                Write-SourceFile -Path $full -Text ($lines -join "`n") -Bom $file.Bom
                Write-Ok ('repaired ' + $path + ' line ' + $number); $repaired++
            }
        }
        if ($repaired -eq 0) { foreach ($line in $check) { Write-Bad $line }; Pop-Location; Invoke-Rollback 'whitespace this pack could not attribute to an owned line' }
        foreach ($relative in $Script:OwnedPaths) { [void](Invoke-NativeLines 'git' @('add', '--', $relative)) }
    }
    [void](Invoke-NativeLines 'git' @('diff', '--cached', '--check'))
    if ($Script:LastNativeExit -ne 0) { Pop-Location; Invoke-Rollback 'the staged view is still not clean after three repair passes' }
    Write-Ok 'index == owned manifest; --check GREEN'

    $allDirtyNow = @(Invoke-NativeLines 'git' @('status', '--porcelain', '--untracked-files=all'))
    $foreignNow = @($allDirtyNow | Where-Object {
        $line = $_.TrimEnd("`r"); if ($line.Length -lt 4) { return $false }
        $path = $line.Substring(3).Trim().Trim('"'); return -not ($Script:OwnedPaths -contains $path)
    })
    if ($foreignNow.Count -ne $Script:ForeignBefore) { Pop-Location; Invoke-Rollback ('foreign worktree entries moved ' + $Script:ForeignBefore + ' -> ' + $foreignNow.Count) }
    Write-Ok ('foreign worktree preserved : ' + $foreignNow.Count + ' entries')
} finally { if ((Get-Location).Path -eq $RepoRoot) { Pop-Location } }

Write-Step 'Phase 9 - re-verify after staging'
$reverify = Invoke-TestFilter -Filter 'FullyQualifiedName~DeclaredDimension|FullyQualifiedName~WorkspaceDeclaredDimension' -LogName 'reverify.log'
if ($reverify.Failed -ne 0) { Invoke-Rollback ('re-verification failed after staging: ' + $reverify.Failed + ' failing') }
Write-Ok ('declared-dimension + workspace suites still GREEN : ' + $reverify.Passed + '/' + $reverify.Total)

# ===========================================================================
if ($Mode -eq 'Validate') {
    Write-Step 'Phase 10 - VALIDATE mode: restoring the tree'
    Push-Location $RepoRoot
    foreach ($relative in $Script:OwnedPaths) { [void](Invoke-NativeLines 'git' @('reset', '--quiet', 'HEAD', '--', $relative)) }
    Pop-Location
    foreach ($relative in $Script:OwnedPaths) {
        $full = Join-Path $RepoRoot ($relative -replace '/', '\')
        $backup = Join-Path $BackupDir (($relative -replace '[\\/]', '__'))
        if (Test-Path -LiteralPath $backup) { Copy-Item -LiteralPath $backup -Destination $full -Force; (Get-Item -LiteralPath $full).LastWriteTime = Get-Date }
        elseif (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Force }
    }
    Write-Ok 'owned paths restored; the repository is exactly as found'
}

Write-Host ''
Write-Host '=== RESULT ==='
Write-Host 'T-094 part 2b stage 2A - the workspace surface carries the keyed declared-dimension contract'
Write-Host ''
Write-Host ('mode                         : ' + $Mode)
Write-Host ('parent SHA                   : ' + $head[0])
Write-Host ('branch                       : ' + $branch[0])
Write-Host  'anchored replacements        : 17, each matched exactly once'
Write-Host  'created files                : 2'
Write-Host ('build                        : GREEN, exit ' + $buildExit)
Write-Host ('declared-dimension suites    : ' + $declaredAfter.Passed + '/' + $declaredAfter.Total)
Write-Host ('stage 2A suite               : ' + $workspace.Passed + '/' + $workspace.Total)
Write-Host ('genericity gate              : ' + $ua08After.Passed + '/' + $ua08After.Total + ' GREEN, no new fingerprint')
Write-Host ('adjacent dashboard/widget    : ' + $adjacent.Passed + '/' + $adjacent.Total)
Write-Host ('JobLog                       : INHERITED, ' + $jobLogAfter.Failed + ' failing of ' + $jobLogAfter.Total + ', identical before and after')
Write-Host ('foreign worktree             : ' + $Script:ForeignBefore + ' entries, preserved')
Write-Host ('evidence                     : ' + $LogDir)
Write-Host ('backups                      : ' + $BackupDir)
Write-Host ''
Write-Host 'ADDITIVE. Legacy named parameters and compiled slots retained on both surfaces.'
Write-Host 'Stage 2B removes them together with the frontend switch, as one valid state.'

if ($Mode -eq 'Commit') {
    Write-Step 'Phase 11 - commit'
    Push-Location $RepoRoot
    try {
        $message = 'T-094 extend the declared-dimension contract to the workspace surface'
        [void](Invoke-NativeLines 'git' @('commit', '-m', $message))
        if ($Script:LastNativeExit -ne 0) { Pop-Location; Write-Bad 'commit failed'; exit 4 }
        $newHead = @(Invoke-NativeLines 'git' @('rev-parse', 'HEAD'))
        Write-Host ''
        Write-Host ('commit SHA                   : ' + $newHead[0])
        Write-Host ('commit subject               : ' + $message)
    } finally { if ((Get-Location).Path -eq $RepoRoot) { Pop-Location } }
}

exit 0
