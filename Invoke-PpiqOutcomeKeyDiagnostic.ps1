<#
    Invoke-PpiqOutcomeKeyDiagnostic.ps1

    PURPOSE
        Settles, with evidence, why the Findings page reads empty while
        ml_correlation_results_v2 reports rows.

        Hypothesis under test:
            The Findings page defaults to outcomeKey 'defect.edge_crack_rate',
            which the Analysis Toolbox does not offer and which no seeded run
            can have produced. If that is true, the empty page is a default
            mismatch, not an RLS tenant mismatch.

    CONTRACT
        READ ONLY. This script writes nothing to the repository and nothing to
        the database. There is no backup stage and no -Revert switch because
        there is nothing to revert.

    RUN FROM REPO ROOT
        powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqOutcomeKeyDiagnostic.ps1
#>

[CmdletBinding()]
param(
    [string[]]$Database = @("ppiq_presentation", "ppiq_app"),
    [string]$PsqlPath   = "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    [string]$DbHost     = "127.0.0.1",
    [string]$DbUser     = "ppiq_dev",
    [string]$DbPassword = "ppiq_dev_local_only",
    [switch]$SkipDatabase
)

$ErrorActionPreference = "Continue"
$script:Findings = New-Object System.Collections.Generic.List[string]

function Write-Section {
    param([string]$Text)
    Write-Host ""
    Write-Host ("=" * 78)
    Write-Host $Text
    Write-Host ("=" * 78)
}

function Write-Sub {
    param([string]$Text)
    Write-Host ""
    Write-Host ("-- " + $Text)
}

# ---------------------------------------------------------------- PREFLIGHT

Write-Section "PREFLIGHT"

$RepoRoot = (Get-Location).Path
Write-Host ("Repo root      : " + $RepoRoot)
Write-Host ("Run at         : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))

$SrcRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web\src"
if (-not (Test-Path $SrcRoot)) {
    Write-Host "FATAL: Frontend\PlantProcess.Web\src not found. Run from the repo root."
    exit 1
}
Write-Host ("Frontend src   : found")

$PsqlAvailable = Test-Path $PsqlPath
if ($PsqlAvailable) {
    Write-Host ("psql           : " + $PsqlPath)
} else {
    Write-Host ("psql           : NOT FOUND at " + $PsqlPath + " (database section will be skipped)")
}

# The five surfaces that carry an outcome / grain / window default.
$Targets = @(
    "Frontend\PlantProcess.Web\src\pages\Analytics\AdvancedAnalysisPage.tsx",
    "Frontend\PlantProcess.Web\src\pages\Phase8\SuggestionRecommendationPage.tsx",
    "Frontend\PlantProcess.Web\src\pages\Analysis\AnalysisToolboxPage.tsx",
    "Frontend\PlantProcess.Web\src\pages\AnalysisJobConfigPage.tsx",
    "Frontend\PlantProcess.Web\src\api\advancedAnalysis.ts"
)

Write-Sub "File report (hash + timestamp, so a later reader knows what was measured)"
foreach ($rel in $Targets) {
    $full = Join-Path $RepoRoot $rel
    if (Test-Path $full) {
        $h = (Get-FileHash -Path $full -Algorithm SHA256).Hash.Substring(0, 16)
        $t = (Get-Item $full).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
        Write-Host ("  " + $h + "  " + $t + "  " + $rel)
    } else {
        Write-Host ("  MISSING                            " + $rel)
        $script:Findings.Add("MISSING FILE: " + $rel)
    }
}

# ------------------------------------------------- SECTION A: STATIC TRUTH

Write-Section "SECTION A - WHAT THE TREE DECLARES"

Write-Sub "A1. Every outcome / grain / window default on the five surfaces"

$LinePattern = 'outcomeKey|OUTCOMES|windowDays|grain\s*=|defect\.[a-z_]+|kpi\.[a-z_]+'
foreach ($rel in $Targets) {
    $full = Join-Path $RepoRoot $rel
    if (-not (Test-Path $full)) { continue }
    Write-Host ""
    Write-Host ("  FILE: " + $rel)
    $hits = Select-String -Path $full -Pattern $LinePattern
    if ($null -eq $hits) {
        Write-Host "    (no matches)"
    } else {
        foreach ($h in $hits) {
            Write-Host ("    " + $h.LineNumber.ToString().PadLeft(5) + ": " + $h.Line.Trim())
        }
    }
}

Write-Sub "A2. Repo-wide inventory of every outcome key literal in the frontend"

$AllKeys = @{}
$tsFiles = Get-ChildItem -Path $SrcRoot -Recurse -Include *.ts, *.tsx -File
foreach ($f in $tsFiles) {
    $matches = Select-String -Path $f.FullName -Pattern '(defect|kpi)\.[a-z0-9_]+' -AllMatches
    foreach ($m in $matches) {
        foreach ($g in $m.Matches) {
            $key = $g.Value
            if (-not $AllKeys.ContainsKey($key)) {
                $AllKeys[$key] = New-Object System.Collections.Generic.List[string]
            }
            $shortPath = $f.FullName.Replace($RepoRoot + "\", "")
            $entry = $shortPath + ":" + $m.LineNumber
            if (-not $AllKeys[$key].Contains($entry)) {
                $AllKeys[$key].Add($entry)
            }
        }
    }
}

foreach ($key in ($AllKeys.Keys | Sort-Object)) {
    Write-Host ""
    Write-Host ("  " + $key + "  (" + $AllKeys[$key].Count + " site(s))")
    foreach ($site in $AllKeys[$key]) {
        Write-Host ("      " + $site)
    }
}

Write-Sub "A3. Consistency verdict"

$toolboxFile = Join-Path $RepoRoot "Frontend\PlantProcess.Web\src\pages\Analysis\AnalysisToolboxPage.tsx"
$findingsFile = Join-Path $RepoRoot "Frontend\PlantProcess.Web\src\pages\Analytics\AdvancedAnalysisPage.tsx"

$offeredKeys = @()
if (Test-Path $toolboxFile) {
    $outcomesLine = Select-String -Path $toolboxFile -Pattern 'OUTCOMES\s*=\s*\[' | Select-Object -First 1
    if ($null -ne $outcomesLine) {
        Write-Host ("  Toolbox OUTCOMES line " + $outcomesLine.LineNumber + ":")
        Write-Host ("    " + $outcomesLine.Line.Trim())
        $om = [regex]::Matches($outcomesLine.Line, '(defect|kpi)\.[a-z0-9_]+')
        foreach ($x in $om) { $offeredKeys += $x.Value }
    } else {
        Write-Host "  Could not locate the OUTCOMES array. Inspect the file by hand."
        $script:Findings.Add("Could not parse OUTCOMES in AnalysisToolboxPage.tsx")
    }
}

$findingsDefault = $null
if (Test-Path $findingsFile) {
    $fd = Select-String -Path $findingsFile -Pattern 'outcomeKey.*\?\?\s*"((defect|kpi)\.[a-z0-9_]+)"' | Select-Object -First 1
    if ($null -ne $fd) {
        $findingsDefault = $fd.Matches[0].Groups[1].Value
        Write-Host ("  Findings page default (line " + $fd.LineNumber + "): " + $findingsDefault)
    } else {
        Write-Host "  Could not locate the Findings page default. Inspect the file by hand."
        $script:Findings.Add("Could not parse the Findings page outcomeKey default")
    }
}

Write-Host ""
if ($null -ne $findingsDefault -and $offeredKeys.Count -gt 0) {
    if ($offeredKeys -contains $findingsDefault) {
        Write-Host ("  CONSISTENT: the Findings default '" + $findingsDefault + "' IS offered by the Toolbox.")
    } else {
        Write-Host ("  MISMATCH CONFIRMED: the Findings default '" + $findingsDefault + "' is NOT in the Toolbox list.")
        Write-Host ("  Toolbox offers: " + ($offeredKeys -join ", "))
        Write-Host "  Consequence: no analysis run from the Toolbox can populate the Findings default view."
        $script:Findings.Add("OUTCOME KEY MISMATCH: Findings defaults to '" + $findingsDefault + "', not offered by the Toolbox")
    }
}

# ---------------------------------------------- SECTION B: DATABASE TRUTH

Write-Section "SECTION B - WHAT THE DATABASE ACTUALLY HOLDS"

if ($SkipDatabase -or (-not $PsqlAvailable)) {
    Write-Host "SKIPPED (either -SkipDatabase was passed or psql was not found)."
    Write-Host "Without this section the diagnosis is static only and cannot be called settled."
    $script:Findings.Add("DATABASE SECTION SKIPPED - diagnosis is static only")
} else {

    $Query = @"
SELECT outcome_key,
       count(*)                                        AS result_rows,
       count(*) FILTER (WHERE tenant_id IS NULL)       AS null_tenant_rows,
       count(DISTINCT tenant_id)                       AS distinct_tenants,
       min(created_at_utc)                             AS first_seen,
       max(created_at_utc)                             AS last_seen
FROM ml_correlation_results_v2
GROUP BY outcome_key
ORDER BY result_rows DESC;
"@

    $TotalQuery = "SELECT count(*) AS total_rows, count(*) FILTER (WHERE tenant_id IS NULL) AS null_tenant_rows FROM ml_correlation_results_v2;"

    foreach ($db in $Database) {
        Write-Sub ("DATABASE: " + $db)
        $conn = "host=" + $DbHost + " dbname=" + $db + " user=" + $DbUser + " password=" + $DbPassword

        Write-Host "  Total rows:"
        & $PsqlPath -d $conn -c $TotalQuery 2>&1 | ForEach-Object { Write-Host ("    " + $_) }
        if ($LASTEXITCODE -ne 0) {
            Write-Host ("    psql exited " + $LASTEXITCODE + " - database unreachable or table absent.")
            $script:Findings.Add("psql failed against " + $db + " (exit " + $LASTEXITCODE + ")")
            continue
        }

        Write-Host ""
        Write-Host "  Rows grouped by outcome_key:"
        & $PsqlPath -d $conn -c $Query 2>&1 | ForEach-Object { Write-Host ("    " + $_) }
        if ($LASTEXITCODE -ne 0) {
            Write-Host ("    psql exited " + $LASTEXITCODE + " on the grouped query.")
            $script:Findings.Add("Grouped query failed against " + $db)
        }
    }
}

# ------------------------------------------------------------- SECTION C

Write-Section "SECTION C - HOW TO READ THE RESULT"

Write-Host @"
  Compare Section A3 against Section B.

  CASE 1  Section B lists outcome keys with rows, and NONE of them is the
          Findings page default from A3.
          -> The empty Findings page is a DEFAULT MISMATCH, not RLS.
             The fix is to put all four surfaces on one shared exported
             constant whose value is a key that actually has rows.

  CASE 2  Section B lists the Findings default WITH rows, and the page is
          still empty in the browser.
          -> The default is innocent. Look at tenant scoping next:
             compare null_tenant_rows and distinct_tenants above against
             the session tenant the API resolves via ppiq_current_tenant().

  CASE 3  Section B returns zero rows in every database.
          -> Neither theory applies. The backfill did not land in the
             database you are demoing from. Check which database the API
             profile is actually pointed at before changing any code.

  Do not change code until this script has told you which case you are in.
"@

Write-Section "SUMMARY"

if ($script:Findings.Count -eq 0) {
    Write-Host "  No structural findings. Read Sections A and B against Section C."
} else {
    foreach ($f in $script:Findings) {
        Write-Host ("  - " + $f)
    }
}

Write-Host ""
Write-Host "Diagnostic complete. Nothing was modified."
Write-Host ""
