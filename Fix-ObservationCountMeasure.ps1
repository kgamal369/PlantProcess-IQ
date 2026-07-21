<#
.SYNOPSIS
    Fix-ObservationCountMeasure.ps1 - registers the 'observationCount' dashboard
    measure so the widget validator stops rejecting it (the 400 "Unsupported
    measure code 'observationCount'" that darkened the Production Overview
    widgets). Full contract: preflight -> backup -> anchored patch (2 files) ->
    self-check the edits landed -> build -> auto-revert on build failure.

.DESCRIPTION
    ROOT CAUSE (read from the tree 20-Jul): three components disagree.
      - system dashboard templates request measureCode "observationCount"
      - DashboardWidgetQueryService CAN serve it (hardcoded string, line ~700)
      - BUT DashboardMetadataCodes.Measures has no ObservationCount constant, so
        it was never added to DashboardWidgetQuerySafetyRegistry.SupportedMeasures
      - the ValidationService runs FIRST and rejects the unknown code -> 400.
    THE FIX (2 additions):
      1. add   public const string ObservationCount = "observationCount";
         to the Measures constants class
      2. add   DashboardMetadataCodes.Measures.ObservationCount,
         to the SupportedMeasures HashSet
    Idempotent: if either line is already present it is left alone.

.PARAMETER RepoRoot   repo root (default = current dir)
.PARAMETER NoBuild    skip the dotnet build gate (patch + verify only)
.PARAMETER Revert     restore both files from the most recent .bak this script made

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-ObservationCountMeasure.ps1
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = (Get-Location).Path,
    [switch]$NoBuild,
    [switch]$Revert
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("Fix_ObservationCount_" + $stamp + ".txt")
$lines   = New-Object System.Collections.Generic.List[string]
$utf8    = New-Object System.Text.UTF8Encoding($false)   # UTF-8 no BOM
function W([string]$t = '') { $lines.Add($t); Write-Host $t }
function Save {
    [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n") + "`r`n"), $utf8)
    Write-Host ''
    Write-Host ('Log: ' + $logPath) -ForegroundColor Cyan
}

# CRLF-preserving read/write helpers (never rewrite line endings we did not touch)
function ReadText([string]$p) { return [System.IO.File]::ReadAllText($p) }
function WriteText([string]$p, [string]$s) { [System.IO.File]::WriteAllText($p, $s, $utf8) }

$codesRel    = 'Backend\PlantProcess.Application\Dashboarding\Contracts\DashboardMetadataDtos.cs'
$registryRel = 'Backend\PlantProcess.Application\Dashboarding\Services\Widgets\DashboardWidgetQuerySafetyRegistry.cs'
$codesPath    = Join-Path $RepoRoot $codesRel
$registryPath = Join-Path $RepoRoot $registryRel

W '=============================================================================='
W ('FIX observationCount MEASURE - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('repo: ' + $RepoRoot)
W '=============================================================================='
W ''

# ---- REVERT mode ------------------------------------------------------------

if ($Revert) {
    W '[REVERT] restoring the most recent backups this script made'
    $any = $false
    foreach ($p in @($codesPath, $registryPath)) {
        $bak = Get-ChildItem -Path (Split-Path $p) -Filter ((Split-Path $p -Leaf) + '.*.bak') -ErrorAction SilentlyContinue |
               Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($bak) {
            Copy-Item -LiteralPath $bak.FullName -Destination $p -Force
            W ('    restored ' + (Split-Path $p -Leaf) + '  <-  ' + $bak.Name)
            $any = $true
        } else {
            W ('    no backup found for ' + (Split-Path $p -Leaf))
        }
    }
    if (-not $any) { W '    nothing to revert.' }
    Save; exit 0
}

# ---- PREFLIGHT --------------------------------------------------------------

W '[PREFLIGHT]'
$fail = $false
foreach ($p in @($codesPath, $registryPath)) {
    if (Test-Path -LiteralPath $p) { W ('    found  ' + $p) }
    else { W ('    MISSING ' + $p); $fail = $true }
}
if ($fail) { W ''; W 'FAIL: expected source files not found. Are you at the repo root?'; Save; exit 2 }

$codes    = ReadText $codesPath
$registry = ReadText $registryPath

# locate the anchors we will patch, on the ACTUAL disk content
$measuresClassHit = ($codes -match '(?s)public static class Measures\s*\{')
$defectCountHit   = ($codes -match 'public const string DefectCount\s*=\s*"defectCount";')
$supportedSetHit  = ($registry -match '(?s)SupportedMeasures\s*=\s*new\(StringComparer\.OrdinalIgnoreCase\)\s*\{')
$regDefectHit     = ($registry -match 'DashboardMetadataCodes\.Measures\.DefectCount\s*,')

W ''
W '[ANCHORS]'
W ('    Measures class present:            ' + $measuresClassHit)
W ('    DefectCount const anchor:          ' + $defectCountHit)
W ('    SupportedMeasures set present:     ' + $supportedSetHit)
W ('    registry DefectCount entry anchor: ' + $regDefectHit)
if (-not ($measuresClassHit -and $defectCountHit -and $supportedSetHit -and $regDefectHit)) {
    W ''
    W 'FAIL: an expected anchor was not found - the files differ from what this'
    W 'fix was built against. NOT patching blind. Send these four booleans + the'
    W 'Measures class and the SupportedMeasures set, and a corrected pack follows.'
    Save; exit 2
}

# ---- already applied? -------------------------------------------------------

$constPresent = $codes    -match 'ObservationCount\s*=\s*"observationCount";'
$setPresent   = $registry -match 'DashboardMetadataCodes\.Measures\.ObservationCount\s*,'
W ''
W '[STATE]'
W ('    constant already present: ' + $constPresent)
W ('    registry entry present:   ' + $setPresent)

if ($constPresent -and $setPresent) {
    W ''
    W 'Already fully applied. Nothing to change. (Rebuild + reload if you have not yet.)'
    Save; exit 0
}

# ---- BACKUP -----------------------------------------------------------------

W ''
W '[BACKUP]'
$codesBak    = $codesPath    + '.' + $stamp + '.bak'
$registryBak = $registryPath + '.' + $stamp + '.bak'
Copy-Item -LiteralPath $codesPath    -Destination $codesBak    -Force; W ('    ' + $codesBak)
Copy-Item -LiteralPath $registryPath -Destination $registryBak -Force; W ('    ' + $registryBak)

# ---- PATCH 1: the constant --------------------------------------------------

W ''
W '[PATCH 1] add ObservationCount constant after DefectCount'
if (-not $constPresent) {
    # match the DefectCount line with its exact leading whitespace, insert a sibling line after it
    $pattern1 = '(?m)^([ \t]*)public const string DefectCount\s*=\s*"defectCount";[ \t]*\r?\n'
    $m1 = [regex]::Match($codes, $pattern1)
    if (-not $m1.Success) { W '    FAIL: DefectCount line not matchable for insert.'; Save; exit 2 }
    $indent = $m1.Groups[1].Value
    $insert = $m1.Value + $indent + 'public const string ObservationCount = "observationCount";' + "`r`n"
    $codes  = $codes.Substring(0, $m1.Index) + $insert + $codes.Substring($m1.Index + $m1.Length)
    WriteText $codesPath $codes
    W '    inserted.'
} else { W '    already present - skipped.' }

# ---- PATCH 2: the registry entry --------------------------------------------

W ''
W '[PATCH 2] add ObservationCount to SupportedMeasures'
if (-not $setPresent) {
    $pattern2 = '(?m)^([ \t]*)DashboardMetadataCodes\.Measures\.DefectCount\s*,[ \t]*\r?\n'
    $m2 = [regex]::Match($registry, $pattern2)
    if (-not $m2.Success) { W '    FAIL: registry DefectCount entry not matchable for insert.'; Save; exit 2 }
    $indent2 = $m2.Groups[1].Value
    $insert2 = $m2.Value + $indent2 + 'DashboardMetadataCodes.Measures.ObservationCount,' + "`r`n"
    $registry = $registry.Substring(0, $m2.Index) + $insert2 + $registry.Substring($m2.Index + $m2.Length)
    WriteText $registryPath $registry
    W '    inserted.'
} else { W '    already present - skipped.' }

# ---- SELF-CHECK: did the edits actually land? -------------------------------

W ''
W '[SELF-CHECK]'
$codes2    = ReadText $codesPath
$registry2 = ReadText $registryPath
$ok1 = $codes2    -match 'ObservationCount\s*=\s*"observationCount";'
$ok2 = $registry2 -match 'DashboardMetadataCodes\.Measures\.ObservationCount\s*,'
W ('    constant present now: ' + $ok1)
W ('    registry entry now:   ' + $ok2)
if (-not ($ok1 -and $ok2)) {
    W '    FAIL: edits did not verify. Restoring backups.'
    Copy-Item -LiteralPath $codesBak    -Destination $codesPath    -Force
    Copy-Item -LiteralPath $registryBak -Destination $registryPath -Force
    W '    reverted.'
    Save; exit 1
}

# ---- BUILD GATE (auto-revert on failure) ------------------------------------

if ($NoBuild) {
    W ''
    W '[BUILD] skipped (-NoBuild). Build + restart the API, then hard-reload the dashboard.'
    Save; exit 0
}

W ''
W '[BUILD] dotnet build (gate; auto-revert if it fails)'
$proj = Join-Path $RepoRoot 'Backend\PlantProcess.Api\PlantProcess.Api.csproj'
if (-not (Test-Path -LiteralPath $proj)) {
    $sln = Get-ChildItem -Path (Join-Path $RepoRoot 'Backend') -Filter *.sln -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($sln) { $proj = $sln.FullName }
}
W ('    target: ' + $proj)
$buildOut = & dotnet build $proj -nologo 2>&1
$buildCode = $LASTEXITCODE
$tail = ($buildOut | Select-Object -Last 12)
foreach ($l in $tail) { W ('      ' + $l) }
if ($buildCode -ne 0) {
    W ''
    W '    BUILD FAILED. Auto-reverting both files to pre-patch state.'
    Copy-Item -LiteralPath $codesBak    -Destination $codesPath    -Force
    Copy-Item -LiteralPath $registryBak -Destination $registryPath -Force
    W '    reverted. No change left on disk. Send the build output above.'
    Save; exit 1
}

W ''
W '    BUILD GREEN. observationCount is now a registered measure.'
W ''
W 'NEXT (manual, 2 steps):'
W '  1. restart the API so the new binary serves:  .\scripts\run\start-api.ps1 -Profile presentation'
W '  2. hard-reload the dashboard (Ctrl-Shift-R) and confirm the widgets render.'
W '     if a DIFFERENT measure code now shows red in the Network tab, run this'
W '     same script pattern for that code (send me its name).'
W ''
W 'Backups kept at:'
W ('  ' + $codesBak)
W ('  ' + $registryBak)
W '(revert anytime:  powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-ObservationCountMeasure.ps1 -Revert)'
Save
exit 0
