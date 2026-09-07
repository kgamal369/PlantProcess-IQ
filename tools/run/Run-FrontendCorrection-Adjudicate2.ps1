<#
================================================================================
PlantProcess IQ - FRONTEND CORRECTION: SECOND ADJUDICATION EXTRACT (READ-ONLY)
================================================================================
Release: R1   Owner: Worker 2 (frontend lane)   Follows: T-205 (closed b7fcc890)
File   : tools\run\Run-FrontendCorrection-Adjudicate2.ps1

WHY THIS SECOND PASS EXISTS

  The first extract settled four of the five findings. Two things it could not
  answer, and neither may be guessed:

  1 THE STANDARDTABLE CONTRACT.
    Section 4 of the first extract printed one line: the file is a barrel that
    re-exports StandardTable.implementation. SpecificationTable is a real data
    table and should become a StandardTable, but its cells carry data-testid,
    data-state, title and a state class each. Whether StandardTable can express
    that decides whether the conversion is a correction or a regression that
    breaks four currently passing tests in specificationTable.test.tsx.

  2 WHETHER THE MECHANICAL GUARD IS SAFE TO WRITE.
    Nine rail prefixes already resolve only to a <Navigate> redirect. A guard
    demanding a real route for every prefix would turn nine of them red for
    reasons unrelated to the three JourneyRail failures. The guard that catches
    the actual defect without that damage is:

        a redirect-only prefix is lawful ONLY IF the route it redirects to is
        itself a match prefix of the SAME stage

    /assistant-config redirects to /assistant/configuration, which is a prefix
    of no stage - so the rule catches exactly the real defect. The other eight
    are EXPECTED to satisfy it, but expected is not measured. This resolves
    every redirect target from App.tsx and reports the rule per prefix.

  Read-only. No test runs, no process starts, nothing in the repository changes.

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

Write-Head 'SECOND ADJUDICATION EXTRACT (READ-ONLY)'
Push-Location -LiteralPath $RepoRootFull
try { $head = (& git rev-parse --short HEAD).Trim() } finally { Pop-Location }
Write-Ok "HEAD $head"
Write-Ok "Evidence: $EvidenceDir"

# ============================================================== QUESTION 1 ====
Write-Head '1 - THE StandardTable CONTRACT, IN FULL'

$stCandidates = @(
    (Join-Path $Web 'src\components\standard\StandardTable.implementation.tsx'),
    (Join-Path $Web 'src\components\standard\StandardTable.implementation.ts'),
    (Join-Path $Web 'src\components\standard\StandardTable.tsx')
)
$stFile = $null
foreach ($c in $stCandidates) { if (Test-Path -LiteralPath $c) { $stFile = $c; break } }

if ($null -eq $stFile) {
    Write-Bad 'StandardTable implementation not found under src\components\standard'
    Write-Inf 'files present there:'
    $dir = Join-Path $Web 'src\components\standard'
    if (Test-Path -LiteralPath $dir) { Get-ChildItem -LiteralPath $dir -File | ForEach-Object { Write-Host "         $($_.Name)" -ForegroundColor DarkGray } }
} else {
    $rel = $stFile.Substring($RepoRootFull.Length + 1)
    Write-Ok ((Get-FileHash -LiteralPath $stFile -Algorithm SHA256).Hash.Substring(0, 16) + '  ' + $rel)
    Copy-Item -LiteralPath $stFile -Destination (Join-Path $EvidenceDir (Split-Path -Leaf $stFile)) -Force
    $lines = [System.IO.File]::ReadAllLines($stFile)
    Write-Inf "$($lines.Count) lines - printing in full, the contract is the whole point"
    for ($i = 0; $i -lt $lines.Count; $i++) { Write-Host ("  {0,5}  {1}" -f ($i + 1), $lines[$i]) -ForegroundColor DarkGray }
}

Write-Head '1b - WHAT SpecificationTable NEEDS FROM IT'
Write-Inf 'per-cell requirements observed in SpecificationTable.tsx lines 105-125:'
Write-Inf '  data-testid="specification-observed"   a stable hook four passing tests already assert on'
Write-Inf '  data-state={entry.state}               drives the passing state assertions'
Write-Inf '  title={...}                            observation count or an explicit not-observed sentence'
Write-Inf '  className per state                    specification-value specification-state--<state>'
Write-Inf 'If the contract above cannot carry all four, converting is a regression, not a correction, and the honest answer is a narrow justified exclusion instead.'

$specTest = Join-Path $Web 'src\components\charts\__tests__\specificationTable.test.tsx'
if (Test-Path -LiteralPath $specTest) {
    Write-Inf ''
    Write-Inf 'the assertions that must survive any conversion:'
    $tl = [System.IO.File]::ReadAllLines($specTest)
    for ($i = 0; $i -lt $tl.Count; $i++) {
        if ($tl[$i] -match 'getBy|getAll|queryBy|toHaveAttribute|data-testid|data-state|title') {
            Write-Host ("  {0,5}  {1}" -f ($i + 1), $tl[$i].Trim()) -ForegroundColor DarkGray
        }
    }
    Copy-Item -LiteralPath $specTest -Destination (Join-Path $EvidenceDir 'specificationTable.test.tsx') -Force
} else { Write-Warn "specificationTable.test.tsx not found at $specTest" }

# ============================================================== QUESTION 2 ====
Write-Head '2 - REDIRECT TARGETS: IS THE PROPOSED GUARD SAFE TO WRITE?'

$railFile = Join-Path $Web 'src\components\journey\JourneyRail.tsx'
$appFile = Join-Path $Web 'src\App.tsx'
if (-not (Test-Path -LiteralPath $railFile)) { Write-Bad "rail not found"; exit 2 }
if (-not (Test-Path -LiteralPath $appFile)) { Write-Bad "App.tsx not found"; exit 2 }

$railText = [System.IO.File]::ReadAllText($railFile)
$appText = [System.IO.File]::ReadAllText($appFile)

$start = $railText.IndexOf('const STAGES: ReadonlyArray<Stage> = [')
$end = $railText.IndexOf("`n];", $start)
$stagesBlock = $railText.Substring($start, $end - $start)

$stages = @()
foreach ($line in ($stagesBlock -split "`n")) {
    if (-not $line.Trim().StartsWith('{ n:')) { continue }
    $n = [int]([regex]::Match($line, 'n:\s*(\d+)').Groups[1].Value)
    $shortLabel = [regex]::Match($line, 'shortLabel:\s*"([^"]+)"').Groups[1].Value
    $raw = [regex]::Match($line, 'match:\s*\[([^\]]*)\]').Groups[1].Value
    $prefixes = @([regex]::Matches($raw, '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    $stages += [pscustomobject]@{ n = $n; shortLabel = $shortLabel; prefixes = $prefixes }
}

# Every <Route path="X">, with its redirect target when the element is a Navigate.
$routes = @()
foreach ($m in [regex]::Matches($appText, 'path=\s*"([^"]+)"')) {
    $path = $m.Groups[1].Value
    $tail = $appText.Substring($m.Index, [Math]::Min(700, $appText.Length - $m.Index))
    $stop = $tail.IndexOf('<Route', 6)
    if ($stop -gt 0) { $tail = $tail.Substring(0, $stop) }
    $isRedirect = $tail.Contains('<Navigate')
    $target = ''
    if ($isRedirect) {
        $tm = [regex]::Match($tail, '<Navigate[^>]*\sto=\s*"([^"]+)"')
        if ($tm.Success) { $target = $tm.Groups[1].Value }
    }
    $routes += [pscustomobject]@{ path = $path.TrimEnd('*').TrimEnd('/'); redirect = $isRedirect; target = $target }
}
Write-Inf "routes parsed: $($routes.Count)"

function Get-RedirectTarget([string]$prefix) {
    foreach ($r in $routes) { if ($r.redirect -and $r.path -eq $prefix) { return $r.target } }
    foreach ($r in $routes) { if ($r.redirect -and $r.path.StartsWith($prefix + '/')) { return $r.target } }
    return ''
}
function Test-RealRoute([string]$p) {
    foreach ($r in $routes) {
        if ($r.redirect) { continue }
        if ($r.path -eq $p) { return $true }
        if ($r.path.StartsWith($p + '/')) { return $true }
        if ($p.StartsWith($r.path + '/') -and $r.path.Length -gt 1) { return $true }
    }
    return $false
}

$rows = @()
$violations = @()
foreach ($s in $stages) {
    foreach ($p in $s.prefixes) {
        if (Test-RealRoute $p) { continue }
        $target = Get-RedirectTarget $p
        $targetInSameStage = $false
        foreach ($q in $s.prefixes) {
            if ($target -eq $q -or ($target -ne '' -and $target.StartsWith($q + '/'))) { $targetInSameStage = $true }
        }
        $verdict = 'LAWFUL ALIAS'
        if ($target -eq '') { $verdict = 'REDIRECT TARGET UNREADABLE' }
        elseif (-not $targetInSameStage) { $verdict = 'VIOLATION' }

        $rows += [pscustomobject]@{ stage = $s.n; prefix = $p; target = $target; verdict = $verdict }
        $colour = 'Gray'
        if ($verdict -eq 'VIOLATION') { $colour = 'Red'; $violations += ("J$($s.n) $p -> $target") }
        elseif ($verdict -ne 'LAWFUL ALIAS') { $colour = 'Yellow' }
        Write-Host ("  {0,-27} J{1,-3} {2,-30} -> {3}" -f $verdict, $s.n, $p, $target) -ForegroundColor $colour
    }
}

Write-Host ''
if ($violations.Count -eq 0) {
    Write-Warn 'no violation found - which would mean the proposed rule does NOT catch the assistant defect. Read the rows above before writing the guard.'
} else {
    Write-Ok ("the rule fires on exactly: " + ($violations -join ', '))
    if ($violations.Count -eq 1) { Write-Ok 'exactly one violation: the guard is safe to write and catches only the real defect' }
    else { Write-Warn "more than one violation: each extra one needs its own ruling before the guard is written" }
}

$payload = [ordered]@{
    task = 'frontend-suite-correction'
    pass = 'second adjudication'
    extractedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    head = $head
    standardTableFile = $(if ($null -ne $stFile) { $stFile.Substring($RepoRootFull.Length + 1) } else { '' })
    redirectOnlyPrefixes = @($rows)
    guardViolations = @($violations)
    proposedRule = 'A rail match prefix that resolves only to a Navigate redirect is lawful only if the route it redirects to is itself a match prefix of the same stage.'
}
$out = Join-Path $EvidenceDir 'frontend-correction-adjudication-2.json'
Write-NoBom $out (($payload | ConvertTo-Json -Depth 10) + "`r`n")

Write-Head 'RESULT'
Write-Ok "adjudication: $out"
Write-Inf 'Nothing was executed and nothing in the repository was touched.'
exit 0
