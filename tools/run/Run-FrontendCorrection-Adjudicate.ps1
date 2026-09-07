<#
================================================================================
PlantProcess IQ - FRONTEND SUITE CORRECTION: ADJUDICATION EXTRACT (READ-ONLY)
================================================================================
Release: R1   Owner: Worker 2 (frontend lane)   Follows: T-205 (closed b7fcc890)
File   : tools\run\Run-FrontendCorrection-Adjudicate.ps1

WHY

  CENTRAL ruled that the five failing tests must be adjudicated, not assumed:
  fix only the side that is actually wrong. Three questions cannot be answered
  from a repository dump three days old, and HEAD has moved since:

    1 does every JourneyRail match prefix resolve to a REAL canonical Route, or
      only to a <Navigate> redirect? CENTRAL wants a mechanical guard for this,
      and a guard written blind would turn other prefixes red for reasons that
      have nothing to do with the three JourneyRail failures.

    2 are the three MetricCard inline styles dynamic values that belong inline,
      or constants that are design debt?

    3 is the raw <table> in SpecificationTable a data table that should be a
      StandardTable, and is the one in HeatmapChart a matrix that should not?

  This runner answers all three from the live tree. It executes no test, starts
  no server, writes nothing inside the repository, and stages nothing. Safe to
  run at any time, including while another worker holds the write window.

EXIT  0 = extract produced   2 = could not run
================================================================================
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = 'C:\Workspace\PlantProcess-IQ',
    [string]$EvidenceRoot = 'C:\Workspace\_ppiq_evidence'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Write-Head([string]$t) { Write-Host ''; Write-Host ('=' * 78) -ForegroundColor DarkCyan; Write-Host $t -ForegroundColor Cyan; Write-Host ('=' * 78) -ForegroundColor DarkCyan }
function Write-Ok  ([string]$t) { Write-Host "  [OK]   $t" -ForegroundColor Green }
function Write-Warn([string]$t) { Write-Host "  [WARN] $t" -ForegroundColor Yellow }
function Write-Bad ([string]$t) { Write-Host "  [FAIL] $t" -ForegroundColor Red }
function Write-Inf ([string]$t) { Write-Host "  [INFO] $t" -ForegroundColor Gray }

function Write-NoBom([string]$Path, [string]$Text) {
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

if (-not (Test-Path -LiteralPath $RepoRoot)) { Write-Bad "Repo not found: $RepoRoot"; exit 2 }
$RepoRootFull = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $RepoRoot).Path).TrimEnd('\')
$Web = Join-Path $RepoRootFull 'Frontend\PlantProcess.Web'

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$EvidenceDir = [System.IO.Path]::GetFullPath((Join-Path (Join-Path $EvidenceRoot 'FrontendCorrection') $stamp)).TrimEnd('\')
New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null

Write-Head 'FRONTEND SUITE CORRECTION - ADJUDICATION EXTRACT (READ-ONLY)'
Push-Location -LiteralPath $RepoRootFull
try { $head = (& git rev-parse --short HEAD).Trim(); $subject = (& git log -1 --pretty=%s).Trim() } finally { Pop-Location }
Write-Ok "HEAD $head  $subject"
Write-Ok "Evidence: $EvidenceDir"

$Files = @{
    Rail        = Join-Path $Web 'src\components\journey\JourneyRail.tsx'
    RailTest    = Join-Path $Web 'src\components\journey\__tests__\JourneyRail.certification.test.tsx'
    RailGuard   = Join-Path $Web 'src\test\architecture\journeyRailCanonical.test.ts'
    App         = Join-Path $Web 'src\App.tsx'
    MetricCard  = Join-Path $Web 'src\components\MetricCard.tsx'
    SpecTable   = Join-Path $Web 'src\components\charts\SpecificationTable.tsx'
    Heatmap     = Join-Path $Web 'src\components\charts\HeatmapChart.tsx'
    RawGuard    = Join-Path $Web 'src\test\architecture\noRawStandardElements.test.ts'
    Ratchet     = Join-Path $Web 'src\test\architecture\uiConformanceRatchet.test.ts'
}

Write-Head '0 - FILE IDENTITY (so the pack is written against exactly these bytes)'
$identity = [ordered]@{}
$missing = @()
foreach ($k in $Files.Keys) {
    $p = $Files[$k]
    if (-not (Test-Path -LiteralPath $p)) { Write-Bad "$k MISSING: $p"; $missing += $k; continue }
    $h = (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash
    $rel = $p.Substring($RepoRootFull.Length + 1)
    $identity[$k] = [ordered]@{ path = $rel; sha256 = $h }
    Write-Inf ($h.Substring(0, 16) + '  ' + $rel)
    Copy-Item -LiteralPath $p -Destination (Join-Path $EvidenceDir ((Split-Path -Leaf $p))) -Force
}
if ($missing.Count -gt 0) { Write-Bad ('cannot adjudicate with files missing: ' + ($missing -join ', ')); exit 2 }

# --------------------------------------------------------------- QUESTION 1 --
Write-Head '1 - EVERY RAIL MATCH PREFIX: REAL ROUTE, REDIRECT ONLY, OR NOTHING'

$railText = [System.IO.File]::ReadAllText($Files.Rail)
$appText = [System.IO.File]::ReadAllText($Files.App)

# Parse the STAGES table.
$start = $railText.IndexOf('const STAGES: ReadonlyArray<Stage> = [')
if ($start -lt 0) { Write-Bad 'STAGES array not found in JourneyRail.tsx'; exit 2 }
$end = $railText.IndexOf("`n];", $start)
$stagesBlock = $railText.Substring($start, $end - $start)

$stages = @()
foreach ($line in ($stagesBlock -split "`n")) {
    if (-not $line.Trim().StartsWith('{ n:')) { continue }
    $n = [int]([regex]::Match($line, 'n:\s*(\d+)').Groups[1].Value)
    $label = [regex]::Match($line, 'label:\s*"([^"]+)"').Groups[1].Value
    $shortLabel = [regex]::Match($line, 'shortLabel:\s*"([^"]+)"').Groups[1].Value
    $to = [regex]::Match($line, 'to:\s*"([^"]*)"').Groups[1].Value
    $raw = [regex]::Match($line, 'match:\s*\[([^\]]*)\]').Groups[1].Value
    $prefixes = @([regex]::Matches($raw, '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    $commissioned = $line -match 'commissioned:\s*true'
    $stages += [pscustomobject]@{ n = $n; label = $label; shortLabel = $shortLabel; to = $to; prefixes = $prefixes; commissioned = $commissioned }
}
Write-Inf "stages parsed: $($stages.Count)"

# Parse every <Route path="..."> and decide whether its element is a redirect.
$routes = @()
foreach ($m in [regex]::Matches($appText, 'path=\s*"([^"]+)"')) {
    $path = $m.Groups[1].Value
    $tail = $appText.Substring($m.Index, [Math]::Min(600, $appText.Length - $m.Index))
    $stop = $tail.IndexOf('<Route')
    if ($stop -gt 0) { $tail = $tail.Substring(0, $stop) }
    $isRedirect = $tail.Contains('<Navigate')
    $routes += [pscustomobject]@{ path = $path; redirect = $isRedirect }
}
$realRoutes = @($routes | Where-Object { -not $_.redirect } | ForEach-Object { $_.path })
$redirectRoutes = @($routes | Where-Object { $_.redirect } | ForEach-Object { $_.path })
Write-Inf "routes parsed: $($routes.Count)  real: $($realRoutes.Count)  redirect: $($redirectRoutes.Count)"

function Resolve-Prefix([string]$prefix) {
    foreach ($r in $realRoutes) {
        $clean = $r.TrimEnd('*').TrimEnd('/')
        if ($clean -eq $prefix) { return 'REAL-EXACT' }
    }
    foreach ($r in $realRoutes) {
        $clean = $r.TrimEnd('*').TrimEnd('/')
        if ($clean.StartsWith($prefix + '/')) { return 'REAL-CHILD' }
        if ($prefix.StartsWith($clean + '/') -and $clean.Length -gt 1) { return 'REAL-PARENT' }
    }
    foreach ($r in $redirectRoutes) {
        $clean = $r.TrimEnd('*').TrimEnd('/')
        if ($clean -eq $prefix -or $clean.StartsWith($prefix + '/')) { return 'REDIRECT-ONLY' }
    }
    return 'NONE'
}

$prefixRows = @()
$redirectOnly = @()
$none = @()
foreach ($s in $stages) {
    foreach ($p in $s.prefixes) {
        $verdict = Resolve-Prefix $p
        $prefixRows += [pscustomobject]@{ stage = $s.n; shortLabel = $s.shortLabel; prefix = $p; verdict = $verdict }
        $colour = 'Gray'
        if ($verdict -eq 'REDIRECT-ONLY') { $colour = 'Yellow'; $redirectOnly += "J$($s.n) $p" }
        elseif ($verdict -eq 'NONE') { $colour = 'Red'; $none += "J$($s.n) $p" }
        Write-Host ("  {0,-14} J{1,-3} {2}" -f $verdict, $s.n, $p) -ForegroundColor $colour
    }
}
Write-Host ''
if ($redirectOnly.Count -eq 0) { Write-Ok 'no rail prefix resolves only to a redirect' }
else { Write-Warn ("prefixes that resolve ONLY to a <Navigate> redirect: " + ($redirectOnly -join ', ')) }
if ($none.Count -eq 0) { Write-Ok 'every rail prefix resolves to something in App.tsx' }
else { Write-Warn ("prefixes with no route at all in App.tsx: " + ($none -join ', ')) }
Write-Inf 'A mechanical guard can only demand REAL-* for the prefixes listed above. Anything already REDIRECT-ONLY or NONE would go red for reasons unrelated to the three JourneyRail failures, so the guard has to be written against this measured reality.'

Write-Head '1b - THE THREE ROUTES THE FAILING TESTS USE'
foreach ($probe in @('/data-integration/connections', '/data-integration/supervisor', '/assistant/configuration', '/assistant-config')) {
    $owner = 'NO STAGE'
    $best = -1
    foreach ($s in $stages) {
        foreach ($p in $s.prefixes) {
            if (($probe -eq $p -or $probe.StartsWith($p + '/')) -and $p.Length -gt $best) { $best = $p.Length; $owner = ("J$($s.n) " + $s.shortLabel + ' - ' + $s.label) }
        }
    }
    $route = 'no route'
    foreach ($r in $routes) { if ($r.path.TrimEnd('*').TrimEnd('/') -eq $probe) { $route = $(if ($r.redirect) { 'REDIRECT' } else { 'REAL ROUTE' }) } }
    Write-Inf ("{0,-32} rail -> {1,-46} App.tsx -> {2}" -f $probe, $owner, $route)
}

# --------------------------------------------------------------- QUESTION 2 --
Write-Head '2 - THE THREE MetricCard INLINE STYLES, IN FULL CONTEXT'
$mcLines = [System.IO.File]::ReadAllLines($Files.MetricCard)
for ($i = 0; $i -lt $mcLines.Count; $i++) {
    if ($mcLines[$i] -notmatch 'style=\{\{') { continue }
    $from = [Math]::Max(0, $i - 4)
    $to = [Math]::Min($mcLines.Count - 1, $i + 10)
    Write-Host ("  --- line " + ($i + 1) + " ---") -ForegroundColor Yellow
    for ($j = $from; $j -le $to; $j++) { Write-Host ("  {0,5}  {1}" -f ($j + 1), $mcLines[$j]) -ForegroundColor DarkGray }
}
Write-Inf 'Decide per block: a value computed from props is legitimately inline; a constant is design debt that belongs in CSS.'

# --------------------------------------------------------------- QUESTION 3 --
Write-Head '3 - THE TWO RAW TABLES, IN FULL CONTEXT'
foreach ($pair in @(@('SpecificationTable', $Files.SpecTable), @('HeatmapChart', $Files.Heatmap))) {
    Write-Host ''
    Write-Host ("  ### " + $pair[0]) -ForegroundColor Yellow
    $lines = [System.IO.File]::ReadAllLines($pair[1])
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -notmatch '<table') { continue }
        $from = [Math]::Max(0, $i - 6)
        $to = [Math]::Min($lines.Count - 1, $i + 34)
        for ($j = $from; $j -le $to; $j++) { Write-Host ("  {0,5}  {1}" -f ($j + 1), $lines[$j]) -ForegroundColor DarkGray }
    }
}
Write-Inf 'Decide per component: a real data table should become StandardTable; a matrix rendered with table markup is not a data table and forcing StandardTable on it would be wrong.'

Write-Head '4 - WHAT StandardTable ACTUALLY OFFERS'
$stPath = Join-Path $Web 'src\components\standard\StandardTable.tsx'
if (Test-Path -LiteralPath $stPath) {
    $st = [System.IO.File]::ReadAllLines($stPath)
    $shown = 0
    for ($i = 0; $i -lt $st.Count -and $shown -lt 45; $i++) {
        if ($st[$i] -match 'export|type |interface |Props|columns|sort|caption') { Write-Host ("  {0,5}  {1}" -f ($i + 1), $st[$i]) -ForegroundColor DarkGray; $shown++ }
    }
} else { Write-Warn "StandardTable not found at $stPath" }

# ------------------------------------------------------------------ OUTPUT ---
$payload = [ordered]@{
    task = 'frontend-suite-correction'
    follows = 'T-205 closed at b7fcc890'
    extractedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    head = $head
    fileIdentity = $identity
    stages = @($stages)
    routeCount = $routes.Count
    realRouteCount = $realRoutes.Count
    redirectRouteCount = $redirectRoutes.Count
    prefixResolution = @($prefixRows)
    redirectOnlyPrefixes = @($redirectOnly)
    unresolvedPrefixes = @($none)
    note = 'Read-only. Nothing in the repository was executed or modified.'
}
$out = Join-Path $EvidenceDir 'frontend-correction-adjudication.json'
Write-NoBom $out (($payload | ConvertTo-Json -Depth 10) + "`r`n")

Write-Head 'RESULT'
Write-Ok "adjudication: $out"
Write-Inf "copies of all nine files: $EvidenceDir"
Write-Inf 'Nothing was executed and nothing in the repository was touched.'
exit 0
