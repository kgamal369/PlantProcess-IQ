<#
    Invoke-PpiqOutcomeAlignmentDiagnostic.ps1

    PURPOSE
        Closes the three unknowns left after Invoke-PpiqOutcomeKeyDiagnostic.ps1,
        so that the fix pack aligns outcome AND grain AND window in one move
        instead of half-fixing and looking like the same bug.

        Q1. What grain and what window do the 320 result rows actually carry?
        Q2. Which database does each API profile actually resolve to?
            (ppiq_app still has all 320 rows tenant-NULL and invisible.)
        Q3. Does the backend declare an outcome registry the frontend should be
            reading, instead of four hardcoded arrays?

    CONTRACT
        READ ONLY. Writes nothing to the repository and nothing to the database.
        No backup stage and no -Revert switch, because nothing is modified.

    RUN FROM REPO ROOT
        powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqOutcomeAlignmentDiagnostic.ps1
#>

[CmdletBinding()]
param(
    [string[]]$Database = @("ppiq_presentation"),
    [string]$PsqlPath   = "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    [string]$DbHost     = "127.0.0.1",
    [string]$DbUser     = "ppiq_dev",
    [string]$DbPassword = "ppiq_dev_local_only",
    [string]$Table      = "ml_correlation_results_v2",
    [switch]$SkipDatabase
)

$ErrorActionPreference = "Continue"
$script:Notes = New-Object System.Collections.Generic.List[string]

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

function Invoke-Psql {
    param([string]$Db, [string]$Sql, [string]$Label)
    $conn = "host=" + $DbHost + " dbname=" + $Db + " user=" + $DbUser + " password=" + $DbPassword
    Write-Host ""
    Write-Host ("  " + $Label)
    & $PsqlPath -d $conn -c $Sql 2>&1 | ForEach-Object { Write-Host ("    " + $_) }
    if ($LASTEXITCODE -ne 0) {
        Write-Host ("    psql exited " + $LASTEXITCODE)
        $script:Notes.Add("psql exit " + $LASTEXITCODE + " on: " + $Label + " (" + $Db + ")")
        return $false
    }
    return $true
}

# ---------------------------------------------------------------- PREFLIGHT

Write-Section "PREFLIGHT"

$RepoRoot = (Get-Location).Path
Write-Host ("Repo root : " + $RepoRoot)
Write-Host ("Run at    : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
Write-Host ("Table     : " + $Table)

if (-not (Test-Path (Join-Path $RepoRoot "Frontend\PlantProcess.Web\src"))) {
    Write-Host "FATAL: run from the repo root."
    exit 1
}

$PsqlAvailable = Test-Path $PsqlPath
if ($PsqlAvailable) {
    Write-Host ("psql      : " + $PsqlPath)
} else {
    Write-Host ("psql      : NOT FOUND at " + $PsqlPath)
}

# ------------------------------------------ Q1: SHAPE OF THE RESULT ROWS

Write-Section "Q1 - WHAT GRAIN AND WINDOW DO THE RESULT ROWS CARRY"

if ($SkipDatabase -or (-not $PsqlAvailable)) {
    Write-Host "SKIPPED."
    $script:Notes.Add("Q1 skipped - grain and window remain unknown, fix pack cannot be aligned")
} else {
    foreach ($db in $Database) {
        Write-Sub ("DATABASE: " + $db)

        # Do not guess column names. Print the schema, then a sample row.
        $colSql = @"
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = '$Table'
ORDER BY ordinal_position;
"@
        [void](Invoke-Psql -Db $db -Sql $colSql -Label "Columns of $Table (so the fix pack references real column names)")

        $sampleSql = "SELECT * FROM $Table ORDER BY created_at_utc DESC LIMIT 2;"
        [void](Invoke-Psql -Db $db -Sql $sampleSql -Label "Two most recent rows in full (actual shape)")

        # Grain and window, if those columns exist. Guarded so a missing column
        # reports honestly instead of aborting the run.
        $grainSql = @"
DO \$\$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
             WHERE table_name = '$Table' AND column_name = 'grain') THEN
    RAISE NOTICE 'grain column exists';
  ELSE
    RAISE NOTICE 'NO grain column on $Table - the grain lives elsewhere, find it before fixing';
  END IF;
END
\$\$;
"@
        [void](Invoke-Psql -Db $db -Sql $grainSql -Label "Does a grain column exist")

        $runSql = @"
SELECT outcome_key, count(*) AS rows
FROM $Table
GROUP BY outcome_key
ORDER BY rows DESC;
"@
        [void](Invoke-Psql -Db $db -Sql $runSql -Label "Outcome keys again, for this run's record")
    }
}

# ---------------------------- Q2: WHICH DATABASE DOES EACH PROFILE RESOLVE TO

Write-Section "Q2 - WHICH DATABASE DOES EACH API PROFILE POINT AT"

Write-Host @"
  This matters more than the code fix. ppiq_app holds all 320 rows with
  tenant_id NULL and zero distinct tenants, so under FORCE-RLS the application
  cannot see any of them. ppiq_presentation holds the same 320 rows correctly
  tenanted. Launching the wrong profile reproduces the empty Findings page no
  matter what the outcome key says.
"@

$ProfileDir = Join-Path $RepoRoot "env\profiles"
if (-not (Test-Path $ProfileDir)) {
    Write-Host ("  env\profiles not found at " + $ProfileDir)
    $script:Notes.Add("env\profiles directory not found")
} else {
    $profiles = Get-ChildItem -Path $ProfileDir -Filter *.env -File
    foreach ($p in $profiles) {
        Write-Sub ("PROFILE: " + $p.Name)
        $hits = Select-String -Path $p.FullName -Pattern 'ConnectionStrings|Database|dbname|Host=|Password'
        if ($null -eq $hits) {
            Write-Host "    (no connection lines found)"
        } else {
            foreach ($h in $hits) {
                $line = $h.Line.Trim()
                # Mask anything after Password= so the output stays pasteable.
                $line = [regex]::Replace($line, '(?i)(password\s*=\s*)([^;""\s]+)', '$1********')
                Write-Host ("    " + $h.LineNumber.ToString().PadLeft(4) + ": " + $line)
            }
        }
    }
}

Write-Sub "How the API is launched (check the profile actually used)"
$startApi = Join-Path $RepoRoot "scripts\run\start-api.ps1"
if (Test-Path $startApi) {
    $h = (Get-FileHash -Path $startApi -Algorithm SHA256).Hash.Substring(0, 16)
    Write-Host ("  " + $h + "  scripts\run\start-api.ps1")
    $defaults = Select-String -Path $startApi -Pattern 'Profile|presentation|local'
    foreach ($d in $defaults) {
        Write-Host ("    " + $d.LineNumber.ToString().PadLeft(4) + ": " + $d.Line.Trim())
    }
} else {
    Write-Host "  scripts\run\start-api.ps1 not found."
}

# ------------------------------- Q3: DOES THE BACKEND DECLARE A REGISTRY

Write-Section "Q3 - DOES THE BACKEND DECLARE AN OUTCOME REGISTRY"

Write-Host @"
  If the engine already declares its own outcome keys, the correct fix is to
  make the frontend read them, not to hardcode a fifth list. If it does not,
  the demo-safe fix is one shared exported constant on the frontend and a
  backlog item for the endpoint.
"@

$BackendRoot = Join-Path $RepoRoot "Backend"
if (-not (Test-Path $BackendRoot)) {
    Write-Host "  Backend directory not found."
    $script:Notes.Add("Backend directory not found")
} else {
    Write-Sub "Backend occurrences of the live outcome namespaces"
    $livePatterns = 'quality\.defect_|downtime\.|kpi\.prime_yield|kpi\.energy_per_ton'
    $csFiles = Get-ChildItem -Path $BackendRoot -Recurse -Include *.cs -File |
               Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
    $found = 0
    foreach ($f in $csFiles) {
        $hits = Select-String -Path $f.FullName -Pattern $livePatterns
        if ($null -ne $hits) {
            $short = $f.FullName.Replace($RepoRoot + "\", "")
            foreach ($h in $hits) {
                Write-Host ("    " + $short + ":" + $h.LineNumber + "  " + $h.Line.Trim())
                $found++
            }
        }
    }
    if ($found -eq 0) {
        Write-Host "    None. The live keys are seeded data only, with no C# declaration."
        $script:Notes.Add("No backend declaration of the live outcome keys - they exist only as data")
    }

    Write-Sub "Any class or endpoint that looks like an outcome registry"
    $regPatterns = 'OutcomeKey|SupportedOutcomes|EngineOutcome|outcomes'
    $regFound = 0
    foreach ($f in $csFiles) {
        $hits = Select-String -Path $f.FullName -Pattern $regPatterns
        if ($null -ne $hits) {
            $short = $f.FullName.Replace($RepoRoot + "\", "")
            foreach ($h in ($hits | Select-Object -First 4)) {
                Write-Host ("    " + $short + ":" + $h.LineNumber + "  " + $h.Line.Trim())
                $regFound++
            }
        }
    }
    if ($regFound -eq 0) {
        Write-Host "    No registry-shaped declaration found."
    }
}

# ------------------------------------------------------------------ CLOSE

Write-Section "WHAT THE FIX PACK WILL DO ONCE THIS RETURNS"

Write-Host @"
  Confirmed already, no further evidence needed:
    - The frontend offers outcome keys in a namespace the engine no longer
      writes. Only kpi.prime_yield overlaps, with 30 rows.
    - The seed is NOT touched. The frontend defaults move to keys that have
      rows.
    - Outcome, grain and window move together across all four surfaces, from
      one shared exported constant.

  Still to be decided by THIS run:
    - The grain and window values the fix must adopt (Q1).
    - Whether the demo profile resolves to ppiq_presentation (Q2). If it
      resolves to ppiq_app, that is a launch-command fix and it outranks the
      code change.
    - Whether the frontend should read a backend registry or carry the
      constant for now (Q3).

  Separate item, deliberately NOT merged into this work:
    - Roughly half of widget queries returning rows=0. Different surface,
      different query path. It is worth testing whether the same namespace
      drift explains it, but that is a hypothesis to test on its own, not an
      assumption to fold into this fix.
"@

Write-Section "NOTES"

if ($script:Notes.Count -eq 0) {
    Write-Host "  No structural notes."
} else {
    foreach ($n in $script:Notes) {
        Write-Host ("  - " + $n)
    }
}

Write-Host ""
Write-Host "Diagnostic complete. Nothing was modified."
Write-Host ""
