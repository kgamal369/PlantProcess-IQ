<#
    Query-PpiqGateDataGap.ps1

    PURPOSE
        Size the two data problems that block the engine, so the fix is targeted
        instead of a reseed.

        GATE 1  Minority-class balance blocks defect.class and defect.severity at
                exactly 0.0%. The code groups outcome rows by category_value and
                returns 0.0 when there are fewer than two distinct values.
                -> Question: how many distinct category_value are there, really?

        GATE 2  Required-field completeness blocks defect.rate_per_m2 at 46.5%
                against an 85% bar. The code defines completeness as:
                    share of outcome rows whose effective_sample_key also appears
                    in ml_feature_values at the same grain
                -> Question: WHICH outcome rows have no matching feature row, and
                   do they cluster in one identifiable cohort (native_grain)?

    WHY THIS MATTERS
        If the missing rows are one cohort, this is a small targeted fix.
        If they are spread evenly, it is a reseed and should not be attempted
        two days before a demo.

    CONTRACT
        READ ONLY. Writes nothing to the repository and nothing to the database.

    RUN FROM REPO ROOT
        powershell -NoProfile -ExecutionPolicy Bypass -File .\Query-PpiqGateDataGap.ps1
#>

[CmdletBinding()]
param(
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    [string]$DbHost     = "127.0.0.1",
    [string]$DbUser     = "ppiq_dev",
    [string]$DbPassword = "ppiq_dev_local_only",
    [string]$Grain      = "coil"
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
Write-Host ("Grain    : " + $Grain)
Write-Host ("Run at   : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
if (-not (Test-Path $PsqlPath)) {
    Write-Host ("FATAL: psql not found at " + $PsqlPath)
    exit 1
}

Write-Section "GATE 1 - WHY MINORITY-CLASS BALANCE IS 0.0%"

Run-Query -Label "1a. defect.class - distinct category values and how many rows each" -Sql @"
SELECT coalesce(category_value, '(null)') AS category_value,
       count(*) AS rows
FROM public.ml_outcome_values
WHERE outcome_key = 'defect.class' AND grain = '$Grain'
GROUP BY category_value
ORDER BY rows DESC
LIMIT 30;
"@

Run-Query -Label "1b. defect.severity - distinct severity values and how many rows each" -Sql @"
SELECT coalesce(severity_value, '(null)') AS severity_value,
       coalesce(category_value, '(null)') AS category_value,
       count(*) AS rows
FROM public.ml_outcome_values
WHERE outcome_key = 'defect.severity' AND grain = '$Grain'
GROUP BY severity_value, category_value
ORDER BY rows DESC
LIMIT 30;
"@

Write-Section "GATE 2 - WHICH OUTCOME ROWS HAVE NO FEATURES"

Run-Query -Label "2a. Headline - how many defect.rate_per_m2 outcome keys join to a feature row" -Sql @"
SELECT count(DISTINCT o.effective_sample_key)                    AS outcome_keys_total,
       count(DISTINCT o.effective_sample_key)
         FILTER (WHERE f.effective_sample_key IS NOT NULL)        AS keys_with_features,
       count(DISTINCT o.effective_sample_key)
         FILTER (WHERE f.effective_sample_key IS NULL)            AS keys_without_features
FROM public.ml_outcome_values o
LEFT JOIN (
    SELECT DISTINCT effective_sample_key
    FROM public.ml_feature_values
    WHERE grain = '$Grain' AND missingness_flag = false
) f ON f.effective_sample_key = o.effective_sample_key
WHERE o.outcome_key = 'defect.rate_per_m2' AND o.grain = '$Grain';
"@

Run-Query -Label "2b. THE KEY QUESTION - are the missing rows one cohort? Break down by native_grain" -Sql @"
SELECT coalesce(o.native_grain, '(null)') AS native_grain,
       count(DISTINCT o.effective_sample_key) AS outcome_keys,
       count(DISTINCT o.effective_sample_key)
         FILTER (WHERE f.effective_sample_key IS NOT NULL) AS with_features,
       count(DISTINCT o.effective_sample_key)
         FILTER (WHERE f.effective_sample_key IS NULL)     AS without_features
FROM public.ml_outcome_values o
LEFT JOIN (
    SELECT DISTINCT effective_sample_key
    FROM public.ml_feature_values
    WHERE grain = '$Grain' AND missingness_flag = false
) f ON f.effective_sample_key = o.effective_sample_key
WHERE o.outcome_key = 'defect.rate_per_m2' AND o.grain = '$Grain'
GROUP BY o.native_grain
ORDER BY without_features DESC;
"@

Run-Query -Label "2c. Same breakdown for defect.class, which scores 100 percent - the contrast" -Sql @"
SELECT coalesce(o.native_grain, '(null)') AS native_grain,
       count(DISTINCT o.effective_sample_key) AS outcome_keys,
       count(DISTINCT o.effective_sample_key)
         FILTER (WHERE f.effective_sample_key IS NOT NULL) AS with_features
FROM public.ml_outcome_values o
LEFT JOIN (
    SELECT DISTINCT effective_sample_key
    FROM public.ml_feature_values
    WHERE grain = '$Grain' AND missingness_flag = false
) f ON f.effective_sample_key = o.effective_sample_key
WHERE o.outcome_key = 'defect.class' AND o.grain = '$Grain'
GROUP BY o.native_grain
ORDER BY outcome_keys DESC;
"@

Write-Section "GATE 2 CONTEXT - WHAT FEATURE DATA EXISTS AT ALL"

Run-Query -Label "3a. Feature rows and distinct sample keys available at this grain" -Sql @"
SELECT count(*)                                AS feature_rows,
       count(DISTINCT effective_sample_key)    AS distinct_sample_keys,
       count(DISTINCT feature_key)             AS distinct_features,
       count(*) FILTER (WHERE missingness_flag = true) AS flagged_missing
FROM public.ml_feature_values
WHERE grain = '$Grain';
"@

Write-Section "HOW TO READ THIS"

Write-Host @"
  GATE 1 (section 1a / 1b)

    ONE ROW ONLY, or everything (null)
      -> The dataset genuinely carries a single defect class. Making these two
         outcomes runnable means authoring real class variety across 51,269
         rows. That is content creation, not a repair. My advice is to leave
         defect.class and defect.severity blocked and let them be the honest
         abstain half of the demo.

    SEVERAL ROWS with a healthy spread
      -> Then the 0.0 percent came from somewhere else and the reading needs to
         change. Send the output before anyone writes anything.

  GATE 2 (section 2b) - this is the one that decides the work

    MISSING ROWS CLUSTER IN ONE OR TWO native_grain VALUES
      -> Small targeted fix. Those cohorts arrived without features. Either
         backfill features for that cohort, or establish whether those rows
         belong in the coil-grain outcome set at all. Either way it is scoped
         and safe to do in an evening.

    MISSING ROWS SPREAD EVENLY ACROSS EVERY native_grain
      -> This is a reseed, not a patch. Do NOT attempt it two days before the
         demo. Ship the abstain story instead and put it in M2.

  Compare 2b against 2c. defect.class scores 100 percent completeness, so its
  cohort clearly has features. The difference between the two lists IS the gap.

  REMINDER BEFORE ANY DATA WRITE
    Back up ppiq_presentation first. A damaged demo database is a worse outcome
    than a blocked gate, and nothing here is worth that risk.
"@

Write-Host ""
Write-Host "Diagnostic complete. Nothing was modified."
Write-Host ""
