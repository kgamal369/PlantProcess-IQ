<#
    Preflight-SharedAuthoringOutputTarget.ps1

    Shared authoring governed output-target authority - measurement runner.
    (Task id belongs in the commit and the evidence, never in a repository filename.)
    REPORT ONLY. This script writes nothing and changes nothing.

    Purpose: measure the exact position T-253 starts from, on the real tree,
    before any pack is authored. Every claim in the T-253 plan is re-proved
    here against the working copy rather than against a dump.

    Sections:
      0  repo identity, HEAD, working-tree cleanliness (the T-243 checkpoint)
      1  material-target literals inside shared authoring (code, not comments)
      2  the graph-mode target path
      3  the SQL-mode target path
      4  what the canonical content actually carries and hashes
      5  candidate target authorities and whether any of them is listable
      6  static-gate infrastructure that a T-253 gate would live in
      7  edit-surface facts for the files a T-253 pack would touch
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"

$Root = (Get-Location).Path
$Fail = 0

function Write-Head($text) {
    Write-Host ""
    Write-Host ("=" * 78)
    Write-Host $text
    Write-Host ("=" * 78)
}
function Write-Ok($text)   { Write-Host ("  [OK]   " + $text) }
function Write-Info($text) { Write-Host ("  [..]   " + $text) }
function Write-Bad($text)  { Write-Host ("  [!!]   " + $text); $script:Fail = $script:Fail + 1 }

function Get-RepoPath($rel) { return (Join-Path $Root $rel) }

function Test-RepoFile($rel) {
    $p = Get-RepoPath $rel
    if (Test-Path -LiteralPath $p) { return $true }
    Write-Bad ("missing: " + $rel)
    return $false
}

# Strips // line comments and /* */ blocks so a literal scan reads CODE and
# never the prose around it. This is the durable form of the guard rule.
function Get-CodeOnly($text, $rel) {
    $t = $text
    if ($rel -match '\.(ts|tsx|cs|js|cjs|mjs)$') {
        $t = [regex]::Replace($t, '(?s)/\*.*?\*/', '')
        $lines = $t -split "`n"
        $kept = @()
        foreach ($line in $lines) { $kept += ($line -replace '//.*$', '') }
        $t = $kept -join "`n"
    }
    return $t
}

function Report-File($rel) {
    $p = Get-RepoPath $rel
    if (-not (Test-Path -LiteralPath $p)) { Write-Bad ("missing: " + $rel); return }
    $bytes = [System.IO.File]::ReadAllBytes($p)
    $text  = [System.IO.File]::ReadAllText($p)
    $hash  = (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash
    $crlf  = ($text -match "`r`n")
    $bom   = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $nonAscii = 0
    foreach ($ch in $text.ToCharArray()) { if ([int]$ch -gt 127) { $nonAscii = $nonAscii + 1 } }
    $lineCount = ($text -split "`n").Count
    $eol = "LF"
    if ($crlf) { $eol = "CRLF" }
    $bomText = "no-BOM"
    if ($bom) { $bomText = "BOM" }
    Write-Info ($rel)
    Write-Host ("         lines=" + $lineCount + "  eol=" + $eol + "  " + $bomText + "  non-ascii=" + $nonAscii)
    Write-Host ("         sha256=" + $hash)
}

function Scan-Code($rel, $pattern, $label) {
    $p = Get-RepoPath $rel
    if (-not (Test-Path -LiteralPath $p)) { Write-Bad ("missing: " + $rel); return 0 }
    $text = [System.IO.File]::ReadAllText($p)
    $code = Get-CodeOnly $text $rel
    $lines = $code -split "`n"
    $n = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $pattern) {
            $n = $n + 1
            $shown = $lines[$i].Trim()
            if ($shown.Length -gt 110) { $shown = $shown.Substring(0, 110) }
            Write-Host ("         " + $rel + ":" + ($i + 1) + "  " + $shown)
        }
    }
    Write-Info ($label + " -> " + $n + " code hit(s) in " + $rel)
    return $n
}

Write-Head "PPIQ T-253 PREFLIGHT - REPORT ONLY - NOTHING IS WRITTEN"
Write-Host ("  repo root : " + $Root)
Write-Host ("  run at    : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))

# ---------------------------------------------------------------- 0
Write-Head "0  REPOSITORY IDENTITY AND THE T-243 CHECKPOINT"

if (-not (Test-Path -LiteralPath (Join-Path $Root ".git"))) {
    Write-Bad "no .git here - this script must run from the repository root"
} else {
    $branch = (& git rev-parse --abbrev-ref HEAD 2>$null)
    $head   = (& git rev-parse --short HEAD 2>$null)
    $subj   = (& git log -1 --pretty=%s 2>$null)
    Write-Info ("branch : " + $branch)
    Write-Info ("HEAD   : " + $head + "  " + $subj)

    # CORRECTED. The first version of this runner judged cleanliness on ANY dirty
    # path, so documentation churn and this script itself reported a false alarm.
    # A checkpoint is about PRODUCT SOURCE. Unrelated documentation stays where it
    # is and is never stashed or deleted to make git status look tidy.
    $status = @(& git status --porcelain 2>$null)
    $owned = @($status | Where-Object { $_ -match '\s(Backend|Frontend|Website)/' })
    if ($owned.Count -eq 0) {
        Write-Ok ("product source CLEAN - a valid checkpoint (" + $status.Count + " unrelated path(s) left alone)")
    } else {
        Write-Bad ($owned.Count.ToString() + " product-source path(s) dirty - do not start a bounded commit on top of these")
        foreach ($line in $owned) { Write-Host ("         " + $line) }
    }
    if ($status.Count -gt 0) {
        Write-Info "unrelated dirty paths, reported and deliberately untouched:"
        $shown = 0
        foreach ($line in $status) {
            if ($shown -ge 40) { Write-Host "         ... (truncated)"; break }
            Write-Host ("         " + $line)
            $shown = $shown + 1
        }
    }
}

# ---------------------------------------------------------------- 1
Write-Head "1  MATERIAL-TARGET LITERALS INSIDE SHARED AUTHORING (CODE ONLY)"

$RelationPattern = "canonical_material_units"
$EntityPattern   = '(\"|'')MaterialUnit(\"|'')'

$AuthoringDir = Get-RepoPath "Frontend\PlantProcess.Web\src\authoring"
if (-not (Test-Path -LiteralPath $AuthoringDir)) {
    Write-Bad "shared authoring folder not found"
} else {
    $srcFiles = @(Get-ChildItem -LiteralPath $AuthoringDir -Recurse -File -Include *.ts, *.tsx |
        Where-Object { $_.Name -notmatch '\.test\.(ts|tsx)$' })
    Write-Info ([string]$srcFiles.Count + " non-test source file(s) under src\authoring")

    $totalRelation = 0
    $totalEntity   = 0
    foreach ($f in $srcFiles) {
        $rel  = $f.FullName.Substring($Root.Length + 1)
        $text = [System.IO.File]::ReadAllText($f.FullName)
        $code = Get-CodeOnly $text $rel
        $lines = $code -split "`n"
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if ($line -match $RelationPattern) {
                $totalRelation = $totalRelation + 1
                Write-Host ("         RELATION  " + $rel + ":" + ($i + 1) + "  " + $line.Trim())
            }
            if ($line -match $EntityPattern) {
                $totalEntity = $totalEntity + 1
                Write-Host ("         ENTITY    " + $rel + ":" + ($i + 1) + "  " + $line.Trim())
            }
        }
    }

    if ($totalRelation -eq 0 -and $totalEntity -eq 0) {
        Write-Ok "no material-target literal in shared authoring code - T-253 already satisfied here"
    } else {
        Write-Bad ("shared authoring carries " + $totalRelation + " physical-relation literal(s) and " + $totalEntity + " canonical-entity literal(s)")
        Write-Host "         these are TWO different namespaces for one idea. T-253 must rule which one"
        Write-Host "         the governed output identity is, not merely replace both with a variable."
    }

    Write-Host ""
    Write-Info "the same scan across TEST files under src\authoring (informational - tests may use fixtures):"
    # CORRECTED. -Include did not filter here and reported SharedAuthoringShell.tsx
    # as a test file. The non-test branch above always used Where-Object on .Name;
    # two filters for one idea, one of them weaker. Now there is one.
    $testFiles = @(Get-ChildItem -LiteralPath $AuthoringDir -Recurse -File -Include *.ts, *.tsx |
        Where-Object { $_.Name -match '\.test\.(ts|tsx)$' })
    $testHits = 0
    foreach ($f in $testFiles) {
        $rel  = $f.FullName.Substring($Root.Length + 1)
        $text = [System.IO.File]::ReadAllText($f.FullName)
        $code = Get-CodeOnly $text $rel
        $lines = $code -split "`n"
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match $RelationPattern -or $lines[$i] -match $EntityPattern) {
                $testHits = $testHits + 1
                Write-Host ("         TEST      " + $rel + ":" + ($i + 1) + "  " + $lines[$i].Trim())
            }
        }
    }
    Write-Info ([string]$testHits + " hit(s) in authoring tests")
}

# ---------------------------------------------------------------- 2
Write-Head "2  THE GRAPH-MODE TARGET PATH"

$ShellRel = "Frontend\PlantProcess.Web\src\authoring\SharedAuthoringShell.tsx"
$GraphRel = "Frontend\PlantProcess.Web\src\authoring\graphSemantics.ts"
$ApiRel   = "Frontend\PlantProcess.Web\src\api\canvasApi.ts"

$null = Scan-Code $ShellRel 'serialisationOutcome\(' "shell calls serialisationOutcome"
$null = Scan-Code $GraphRel 'targetEntity' "graphSemantics targetEntity parameter/use"
$null = Scan-Code $ApiRel   'targetEntity' "MapperGraph.targetEntity in the API contract"

Write-Host ""
Write-Info "reading: the graph target is the second argument to serialisationOutcome and travels"
Write-Info "inside MapperGraph.targetEntity, which IS part of the persisted graph JSON."

# ---------------------------------------------------------------- 3
Write-Head "3  THE SQL-MODE TARGET PATH"

$null = Scan-Code $ShellRel 'canonicalEntity' "shell doSaveSql canonicalEntity"
$null = Scan-Code $ApiRel   'canonicalEntity' "saveSqlVersion canonicalEntity in the API contract"

$EndpointRel  = "Backend\PlantProcess.Api\Endpoints\Prep\AuthoringSupportEndpoints.cs"
$LifecycleRel = "Backend\PlantProcess.Application\Definitions\Canvas\ICanvasDefinitionLifecycle.cs"

$null = Scan-Code $EndpointRel  'CanonicalEntity' "save endpoint CanonicalEntity"
$null = Scan-Code $LifecycleRel 'CanonicalEntity' "lifecycle contract CanonicalEntity"

Write-Host ""
Write-Info "reading: CanonicalEntity reaches the lifecycle only inside CanvasProjectionHandles."

$p = Get-RepoPath $LifecycleRel
if (Test-Path -LiteralPath $p) {
    $t = [System.IO.File]::ReadAllText($p)
    if ($t -match 'not part of the hashed content') {
        Write-Bad "CONFIRMED: the SQL target is a projection handle and is NOT hashed canonical content"
        Write-Host "         so it cannot survive save/version/reopen as T-253 requires, today."
    } else {
        Write-Info "the 'not part of the hashed content' sentence was not found - re-read the contract by hand"
    }
}

# ---------------------------------------------------------------- 4
Write-Head "4  WHAT THE CANONICAL CONTENT CARRIES AND HASHES"

$ContentRel = "Backend\PlantProcess.Application\Definitions\Canvas\CanvasDefinitionContent.cs"
if (Test-RepoFile $ContentRel) {
    $t = [System.IO.File]::ReadAllText((Get-RepoPath $ContentRel))
    $code = Get-CodeOnly $t $ContentRel

    Write-Info "keys assigned into the content root, in code order:"
    $lines = $code -split "`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '\["(\w+)"\]\s*=') {
            Write-Host ("         " + $ContentRel + ":" + ($i + 1) + "  " + $lines[$i].Trim())
        }
    }

    $sqlHasTarget = $false
    if ($code -match '(?s)public static string ForSql.*?return Serialise') {
        $sqlBody = $Matches[0]
        if ($sqlBody -match 'target|Target|canonicalEntity|CanonicalEntity') { $sqlHasTarget = $true }
    }
    if ($sqlHasTarget) {
        Write-Ok "ForSql already writes a target key into hashed content"
    } else {
        Write-Bad "ForSql writes NO target key - SQL-mode content has no slot for a governed output identity"
    }

    if ($code -match '(?s)RepresentationSql\s*=>\s*new CanvasDefinitionRepresentation.*?\)') {
        Write-Info "Read() SQL branch returns:"
        Write-Host ("         " + (($Matches[0] -replace "`r", " ") -replace "`n", " " -replace '\s+', ' '))
    }
    Write-Host ""
    Write-Info "CONSEQUENCE TO RULE ON: adding a key to hashed content changes the definition hash,"
    Write-Info "so an unchanged existing definition re-saved after T-253 becomes a NEW version."
}

# ---------------------------------------------------------------- 5
Write-Head "5  CANDIDATE TARGET AUTHORITIES - IS ANY OF THEM LISTABLE TODAY"

$CatalogRel = "Backend\PlantProcess.Application\Common\Canonical\ICanonicalEntityCatalog.cs"
if (Test-RepoFile $CatalogRel) {
    $t = Get-CodeOnly ([System.IO.File]::ReadAllText((Get-RepoPath $CatalogRel))) $CatalogRel
    Write-Info "ICanonicalEntityCatalog members:"
    foreach ($m in [regex]::Matches($t, '(?m)^\s*[\w\?\<\>\[\], ]+\s+\w+\(')) {
        Write-Host ("         " + $m.Groups[0].Value.Trim())
    }
    if ($t -match 'IEnumerable|IReadOnlyList|IReadOnlyCollection') {
        Write-Ok "the catalogue exposes an enumeration member"
    } else {
        Write-Bad "the catalogue exposes NO enumeration - the shell cannot offer a list of governed targets from it today"
    }
}

$SubjectRel = "Backend\PlantProcess.Api\Endpoints\AnalysisSubjects\AnalysisSubjectEndpoints.cs"
if (Test-RepoFile $SubjectRel) {
    $t = Get-CodeOnly ([System.IO.File]::ReadAllText((Get-RepoPath $SubjectRel))) $SubjectRel
    Write-Info "Analysis Subject routes:"
    foreach ($m in [regex]::Matches($t, 'group\.Map(Get|Post|Put|Delete)\("([^"]*)"')) {
        Write-Host ("         " + $m.Groups[1].Value.ToUpper() + " /api/analysis-subjects" + $m.Groups[2].Value)
    }
    Write-Info "if there is no collection GET above, Analysis Subject cannot populate a picker either."
}

$MapSvcRel = "Backend\PlantProcess.Application\Integration\Services\Mapping\MappingDefinitionService.cs"
if (Test-Path -LiteralPath (Get-RepoPath $MapSvcRel)) {
    $t = Get-CodeOnly ([System.IO.File]::ReadAllText((Get-RepoPath $MapSvcRel))) $MapSvcRel
    if ($t -match '(?s)AllowedTargetEntities\s*=\s*new[^\{]*\{(.*?)\}') {
        $names = @([regex]::Matches($Matches[1], '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
        Write-Bad ("a THIRD target vocabulary exists: AllowedTargetEntities, " + $names.Count + " hardcoded name(s)")
        Write-Host ("         " + ($names -join ", "))
        Write-Host "         out of the T-253 file lock, but it is the reason a picker must not be built on it."
    }
}

# ---------------------------------------------------------------- 6
Write-Head "6  STATIC-GATE INFRASTRUCTURE A T-253 GATE WOULD LIVE IN"

$GenRel  = "Backend\tests\PlantProcess.Architecture.Tests\ScopeAwareGenericity.cs"
$TermRel = "Backend\tests\PlantProcess.Architecture.Tests\plant_vocabulary_terms.json"

if (Test-RepoFile $GenRel) {
    $t = Get-CodeOnly ([System.IO.File]::ReadAllText((Get-RepoPath $GenRel))) $GenRel
    if ($t -match 'ProductExtensions\s*=\s*\{([^\}]*)\}') {
        Write-Info ("genericity gate scans extensions: " + ($Matches[1].Trim()))
    }
    if ($t -match '"Backend",\s*"Frontend"') {
        Write-Ok "the gate already walks BOTH Backend and Frontend - a T-253 gate needs no new scanner"
    } else {
        Write-Info "confirm the gate scan areas by hand"
    }
}

if (Test-RepoFile $TermRel) {
    $terms = (Get-Content -LiteralPath (Get-RepoPath $TermRel) -Raw | ConvertFrom-Json)
    $names = @($terms.terms | ForEach-Object { $_.term })
    Write-Info ("plant_vocabulary_terms.json carries " + $names.Count + " term(s): " + ($names -join ", "))
    if ($names -contains "MaterialUnit") {
        Write-Info "MaterialUnit is already a registered term"
    } else {
        Write-Ok "MaterialUnit is NOT a registered UA-08 term"
        Write-Host "         adding it globally would fire far outside shared authoring. A T-253 gate must be"
        Write-Host "         PATH-SCOPED to src\authoring, not a new global vocabulary term."
    }
}

$RatchetRel = "Frontend\PlantProcess.Web\src\test\architecture\uiConformance.baseline.json"
if (Test-Path -LiteralPath (Get-RepoPath $RatchetRel)) {
    $baseline = (Get-Content -LiteralPath (Get-RepoPath $RatchetRel) -Raw | ConvertFrom-Json)
    $keys = @($baseline.PSObject.Properties.Name)
    $authoringKeys = @($keys | Where-Object { $_ -match 'authoring' })
    Write-Info ([string]$keys.Count + " file(s) in the uiConformance baseline; " + [string]$authoringKeys.Count + " under authoring")
    if ($authoringKeys.Count -eq 0) {
        Write-Bad "SharedAuthoringShell has NO baseline entry - it starts at ZERO raw controls"
        Write-Host "         any target picker added by T-253 must use StandardP2Select and no raw label element."
    }
}

# ---------------------------------------------------------------- 7
Write-Head "7  EDIT SURFACE - FILES A T-253 PACK WOULD TOUCH"

$Surface = @(
    $ShellRel,
    $ApiRel,
    $GraphRel,
    $EndpointRel,
    $LifecycleRel,
    $ContentRel,
    "Frontend\PlantProcess.Web\src\authoring\sharedAuthoringShell.test.tsx",
    "Frontend\PlantProcess.Web\src\authoring\sqlModeShell.test.tsx",
    "Frontend\PlantProcess.Web\src\authoring\s2ShellSave.test.tsx"
)
foreach ($rel in $Surface) { Report-File $rel }

# ---------------------------------------------------------------- summary
Write-Head "SUMMARY"
if ($Fail -eq 0) {
    Write-Host "  no blocking observation recorded by this preflight."
} else {
    Write-Host ("  " + $Fail + " observation(s) marked [!!]. Each is EVIDENCE, not a script failure:")
    Write-Host "  this preflight is REPORT ONLY and changed nothing."
}
Write-Host ""
Write-Host "  Paste this whole output back if anything here differs from the recorded position."
Write-Host ""
