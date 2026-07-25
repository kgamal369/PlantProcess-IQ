<#
    Query-PpiqOrphanOutcomeValues.ps1

    PURPOSE
        One question. The registry (ml_outcome_definitions) declares 8 outcomes.
        The results table carries 5 keys it does not declare, holding 260 of the
        320 result rows:

            quality.defect_hold_binary        112
            quality.defect_rate_per_m2         70
            downtime.equipment_stoppage_min    30
            downtime.production_stoppage_min   30
            downtime.cascade_amplified_flag    18

        Do those 5 keys have underlying rows in ml_outcome_values (195,221 rows),
        and at what grain?

    WHY IT DECIDES THE FIX
        HAVE VALUES  -> the engine genuinely computes these outcomes and the
                        registry simply was never updated. A metadata-only seed
                        of 5 rows into ml_outcome_definitions is correct, safe,
                        and unlocks the 112-row headline outcome for the demo.

        NO VALUES    -> the 260 result rows are orphaned history from a retired
                        engine version. Declaring those keys would put entries
                        in the dropdown that run and then BLOCK, which is worse
                        in the room than a shorter honest list. In that case the
                        demo runs on kpi.prime_yield / kpi.energy_per_ton and the
                        engine-vs-registry divergence becomes an M2 item.

    CONTRACT
        READ ONLY. Writes nothing to the repository and nothing to the database.
        No dollar-quoted blocks. Every column referenced was read from the live
        information_schema output of the previous pack, not assumed.

    RUN FROM REPO ROOT
        powershell -NoProfile -ExecutionPolicy Bypass -File .\Query-PpiqOrphanOutcomeValues.ps1
#>

[CmdletBinding()]
param(
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    [string]$DbHost     = "127.0.0.1",
    [string]$DbUser     = "ppiq_dev",
    [string]$DbPassword = "ppiq_dev_local_only"
)

$ErrorActionPreference = "Continue"
$conn = "host=" + $DbHost + " dbname=" + $Database + " user=" + $DbUser + " password=" + $DbPassword

function Write-Section {
    param([string]$Text)
    Write-Host ""
    Write-Host ("=" * 78)
    Write-Host $Text
    Write-Host ("=" * 78)
}

function Run-Query {
    param([string]$Label, [string]$Sql)
    Write-Host ""
    Write-Host ("  " + $Label)
    & $PsqlPath -d $conn -c $Sql 2>&1 | ForEach-Object { Write-Host ("    " + $_) }
    if ($LASTEXITCODE -ne 0) {
        Write-Host ("    psql exited " + $LASTEXITCODE)
    }
}

Write-Section "PREFLIGHT"
Write-Host ("Database : " + $Database)
Write-Host ("Run at   : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
if (-not (Test-Path $PsqlPath)) {
    Write-Host ("FATAL: psql not found at " + $PsqlPath)
    exit 1
}

Write-Section "1 - EVERY OUTCOME KEY THAT HAS VALUES, WITH ITS GRAIN"

Run-Query -Label "1a. Value volume and grain per outcome key (this is the ground truth)" -Sql @"
SELECT outcome_key,
       grain,
       native_grain,
       count(*)                       AS value_rows,
       count(DISTINCT outcome_version) AS versions,
       count(*) FILTER (WHERE outcome_definition_id IS NULL) AS unlinked_rows,
       min(observed_at_utc)           AS first_observed,
       max(observed_at_utc)           AS last_observed
FROM public.ml_outcome_values
GROUP BY outcome_key, grain, native_grain
ORDER BY value_rows DESC;
"@

Write-Section "2 - THE FIVE ORPHANS SPECIFICALLY"

Run-Query -Label "2a. Do the orphan keys have values at all" -Sql @"
SELECT k.outcome_key,
       coalesce(v.value_rows, 0) AS value_rows,
       v.grain,
       CASE WHEN coalesce(v.value_rows, 0) > 0
            THEN 'HAS VALUES - safe to declare'
            ELSE 'NO VALUES - orphaned history'
       END AS verdict
FROM (VALUES
        ('quality.defect_hold_binary'),
        ('quality.defect_rate_per_m2'),
        ('downtime.equipment_stoppage_min'),
        ('downtime.production_stoppage_min'),
        ('downtime.cascade_amplified_flag')
     ) AS k(outcome_key)
LEFT JOIN (
    SELECT outcome_key, grain, count(*) AS value_rows
    FROM public.ml_outcome_values
    GROUP BY outcome_key, grain
) v ON v.outcome_key = k.outcome_key
ORDER BY value_rows DESC, k.outcome_key;
"@

Write-Section "3 - THE OTHER DIRECTION: DECLARED OUTCOMES WITH NO VALUES"

Run-Query -Label "3a. Registry entries that have no underlying values (dead dropdown entries today)" -Sql @"
SELECT d.outcome_key,
       d.grain AS declared_grain,
       d.status,
       coalesce(v.value_rows, 0) AS value_rows
FROM public.ml_outcome_definitions d
LEFT JOIN (
    SELECT outcome_key, count(*) AS value_rows
    FROM public.ml_outcome_values
    GROUP BY outcome_key
) v ON v.outcome_key = d.outcome_key
WHERE d.is_deleted = false
ORDER BY value_rows DESC, d.outcome_key;
"@

Write-Section "4 - GRAIN SANITY FOR THE TWO KEYS THAT WORK TODAY"

Run-Query -Label "4a. kpi.prime_yield and kpi.energy_per_ton - declared grain vs value grain" -Sql @"
SELECT d.outcome_key,
       d.grain AS declared_grain,
       v.grain AS value_grain,
       count(v.*) AS value_rows
FROM public.ml_outcome_definitions d
LEFT JOIN public.ml_outcome_values v ON v.outcome_key = d.outcome_key
WHERE d.is_deleted = false
  AND d.outcome_key IN ('kpi.prime_yield', 'kpi.energy_per_ton')
GROUP BY d.outcome_key, d.grain, v.grain
ORDER BY d.outcome_key;
"@

Write-Section "HOW TO READ THIS"

Write-Host @"
  Section 2a gives the verdict directly.

  If the orphans show HAS VALUES:
      The engine computes them, the registry was never updated, and the fix is
      two parts:
        1. Metadata-only seed: 5 rows into ml_outcome_definitions with the grain
           and type taken from section 1a. No data seed is touched.
        2. Wiring: Findings page and Analysis Toolbox read
           GET /ml/foundation/outcomes; grain comes from the registry row; the
           frontend windowDays = 30 defaults are deleted so the backend 3650
           applies.
      Result: all 320 result rows reachable, dropdowns fully data-backed, the
      Rule 2 steel-specific arrays retired.

  If the orphans show NO VALUES:
      Do NOT seed them. The demo runs on kpi.prime_yield and kpi.energy_per_ton,
      which are declared and have values. Ship the wiring half only, and file the
      engine-vs-registry divergence as an M2 item with these numbers attached.
      The room sentence is honest and short: the engine writes outcomes the
      catalogue has not yet adopted.

  Section 3a is worth reading either way. Any registry entry with zero values is
  a dropdown option that produces an empty page today. Those are candidates for
  status change rather than deletion, so the catalogue stays honest.

  Section 4a is a trap check. If declared_grain and value_grain disagree for the
  two working keys, then passing grain=generic in the URL is not enough and the
  grain story needs settling before anything is wired.
"@

Write-Host ""
Write-Host "Diagnostic complete. Nothing was modified."
Write-Host ""
