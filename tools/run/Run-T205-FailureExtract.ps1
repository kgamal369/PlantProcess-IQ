<#
================================================================================
PlantProcess IQ - T-205 FAILURE OWNERSHIP EXTRACT (READ-ONLY)
================================================================================
Backlog task : T-205   Release: R1   Owner: Worker 2 (Frontend test infrastructure)
File         : tools\run\Run-T205-FailureExtract.ps1

WHY

  The preflight measured the suite: 850 total, 845 passed, 5 failed, 0 skipped,
  terminating on its own in 678 s with exit 1. CENTRAL section 11 says a real
  product failure exposed by the suite must be named, attributed to its owning
  task, reported, and NOT absorbed into T-205.

  To attribute it I need the failure messages, not the test titles. They are
  already on disk in the JSON report the preflight wrote. This reads that file
  and prints them. It executes no test, starts no process, and touches nothing
  in the repository.

  It also states, for each failing file, whether the working tree currently
  holds an uncommitted modification that could plausibly be the cause - as a
  QUESTION for the owner, never as a verdict.

USAGE
  -ReportPath  the vitest-report.json from a preflight run. If omitted, the
               newest one under the evidence root is used.

EXIT  0 = extract produced   2 = no report found
================================================================================
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = 'C:\Workspace\PlantProcess-IQ',
    [string]$EvidenceRoot = 'C:\Workspace\_ppiq_evidence',
    [string]$ReportPath = ''
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

function Get-Prop($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $p = $Object.PSObject.Properties[$Name]
    if ($null -eq $p) { return $null }
    return $p.Value
}

Write-Head 'T-205 FAILURE OWNERSHIP EXTRACT  -  READ-ONLY'

if ($ReportPath -eq '') {
    $base = Join-Path $EvidenceRoot 'T205Preflight'
    if (-not (Test-Path -LiteralPath $base)) { Write-Bad "no preflight evidence under $base"; exit 2 }
    $found = Get-ChildItem -LiteralPath $base -Recurse -Filter 'vitest-report.json' -File -ErrorAction SilentlyContinue |
             Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $found) { Write-Bad "no vitest-report.json under $base"; exit 2 }
    $ReportPath = $found.FullName
}
if (-not (Test-Path -LiteralPath $ReportPath)) { Write-Bad "report not found: $ReportPath"; exit 2 }
Write-Ok "report: $ReportPath"

$RepoRootFull = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $RepoRoot).Path).TrimEnd('\')
Push-Location -LiteralPath $RepoRootFull
try { $dirty = @(& git status --porcelain) } finally { Pop-Location }
$dirtyPaths = @()
foreach ($d in $dirty) {
    $p = $d.Substring(3).Trim().Trim('"')
    $dirtyPaths += $p
}

$report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json

Write-Head 'COUNTS AS THE RUNNER REPORTED THEM'
Write-Inf ("total   " + (Get-Prop $report 'numTotalTests'))
Write-Inf ("passed  " + (Get-Prop $report 'numPassedTests'))
Write-Inf ("failed  " + (Get-Prop $report 'numFailedTests'))
Write-Inf ("pending " + (Get-Prop $report 'numPendingTests'))
Write-Inf ("suites  " + (Get-Prop $report 'numTotalTestSuites'))

Write-Head 'EVERY FAILING TEST, WITH ITS MESSAGE'

$records = @()
$suites = @(Get-Prop $report 'testResults')
foreach ($s in $suites) {
    $file = [string](Get-Prop $s 'name')
    $rel = $file
    if ($file.StartsWith($RepoRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        $rel = $file.Substring($RepoRootFull.Length + 1).Replace('/', '\')
    }
    $rel = $rel.Replace('/', '\')

    foreach ($t in @(Get-Prop $s 'assertionResults')) {
        $status = [string](Get-Prop $t 'status')
        if ($status -ne 'failed') { continue }

        $title = [string](Get-Prop $t 'fullName')
        if ($title -eq '') { $title = [string](Get-Prop $t 'title') }
        $messages = @(Get-Prop $t 'failureMessages')

        Write-Host ''
        Write-Bad "$rel"
        Write-Host ("         " + $title) -ForegroundColor Yellow
        foreach ($m in $messages) {
            foreach ($line in ($m -split "`n")) {
                if ($line.Trim().Length -eq 0) { continue }
                Write-Host ("         " + $line.TrimEnd()) -ForegroundColor DarkGray
            }
        }

        # Is anything uncommitted that could plausibly explain this? A QUESTION
        # for the owning task, never a verdict from here.
        $suspects = @()
        foreach ($p in $dirtyPaths) {
            if ($p -like '*.tsx' -or $p -like '*.ts') { $suspects += $p }
        }
        if ($suspects.Count -gt 0) {
            Write-Warn ("uncommitted source in the tree that an owner should rule on first: " + ($suspects -join ', '))
        }

        $records += [pscustomobject]@{
            file = $rel
            test = $title
            message = ($messages -join "`n")
        }
    }
}

if ($records.Count -eq 0) { Write-Ok 'no failing tests in this report' }

Write-Head 'FAILING FILES, GROUPED'
$byFile = $records | Group-Object -Property file
foreach ($g in $byFile) {
    Write-Host ("  $($g.Count)  $($g.Name)") -ForegroundColor Yellow
}

Write-Head 'UNCOMMITTED WORKING TREE AT THE TIME OF THIS EXTRACT'
if ($dirty.Count -eq 0) { Write-Ok 'working tree clean' }
else { foreach ($d in $dirty) { Write-Host "         $d" -ForegroundColor DarkGray } }

$outDir = Split-Path -Parent $ReportPath
$outPath = Join-Path $outDir 't205-failures.json'
$payload = [ordered]@{
    task = 'T-205'
    extractedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    reportPath = $ReportPath
    total = (Get-Prop $report 'numTotalTests')
    passed = (Get-Prop $report 'numPassedTests')
    failed = (Get-Prop $report 'numFailedTests')
    pending = (Get-Prop $report 'numPendingTests')
    failures = @($records)
    workingTree = @($dirty)
    note = 'T-205 owns the gate, not these failures. Each entry needs an owning task before T-205 can claim a GREEN normal run.'
}
Write-NoBom $outPath (($payload | ConvertTo-Json -Depth 8) + "`r`n")

Write-Head 'RESULT'
Write-Ok "extract written: $outPath"
Write-Inf 'Nothing was executed and nothing in the repository was touched.'
exit 0
