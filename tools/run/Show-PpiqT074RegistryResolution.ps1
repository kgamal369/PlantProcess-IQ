<#
    PPIQ runner: Show-PpiqT074RegistryResolution

    READ-ONLY. Writes nothing, changes nothing, takes no -Apply.

    T-074 says: measure which non-synthetic active registry row resolves from a
    natural question containing a quantity name, and do NOT assert a hardcoded
    code before measuring. This is that measurement.

    It applies the SAME normalisation the guard will use - trim, lowercase,
    replace dot, underscore and hyphen with spaces, collapse whitespace - to the
    registry's OWN parameter_code and parameter_name, and reports which of those
    normalised phrases appear in a natural question. It hardcodes no vocabulary:
    the phrases come out of the registry, and the question is a parameter you can
    change.

    What it is looking for, and why it matters here: the presentation registry is
    known to carry legacy demo vocabulary alongside configured vocabulary. If both
    resolve from the same question, the guard must prefer the real configured
    definition over the synthetic one, and if two REAL definitions tie, the answer
    is ambiguous and the guard must not guess. This prints exactly that picture.
#>

[CmdletBinding()]
param(
    [string]$Question = "what is the casting speed",
    [string]$PgDb = ""
)

$ErrorActionPreference = "Continue"

function W([string]$Text) { Write-Host $Text }
function Head([string]$Text) {
    W ""
    W ("-" * 78)
    W $Text
    W ("-" * 78)
}

if (-not (Test-Path "Jenkinsfile")) { W "FAIL  run from the repository root"; exit 1 }

$PgUser = "ppiq_dev"; $PgPass = "ppiq_dev_local_only"
if ([string]::IsNullOrWhiteSpace($PgDb)) { $PgDb = "ppiq_presentation" }
$EnvFile = "env\profiles\presentation.env"
if (Test-Path $EnvFile) {
    foreach ($line in (Get-Content $EnvFile)) {
        if ($line -match '^\s*#') { continue }
        if ($line -match 'POSTGRES_USER\s*=\s*(.+)$')     { $PgUser = $matches[1].Trim() }
        if ($line -match 'POSTGRES_PASSWORD\s*=\s*(.+)$') { $PgPass = $matches[1].Trim() }
        if ($line -match 'POSTGRES_DB\s*=\s*(.+)$')       { $PgDb   = $matches[1].Trim() }
    }
}
$env:PGPASSWORD = $PgPass

if ($null -eq (Get-Command psql -ErrorAction SilentlyContinue)) { W "FAIL  psql is not on PATH"; exit 1 }

# The question, normalised the same mechanical way the registry phrases are.
$NormalisedQuestion = ($Question.ToLowerInvariant() -replace '[._-]', ' ') -replace '\s+', ' '
$NormalisedQuestion = $NormalisedQuestion.Trim()

W ("Database  : " + $PgDb)
W ("Question  : " + $Question)
W ("Normalised: " + $NormalisedQuestion)

function Show([string]$Title, [string]$Query) {
    Head $Title
    & psql -h 127.0.0.1 -p 5432 -U $PgUser -d $PgDb -w -X -v ON_ERROR_STOP=1 -c $Query
}

Show "1. WHAT THE REGISTRY HOLDS" @"
SELECT count(*)                                        AS rows,
       count(*) FILTER (WHERE is_deleted)              AS deleted,
       count(*) FILTER (WHERE is_synthetic)            AS synthetic,
       count(*) FILTER (WHERE NOT is_deleted AND NOT is_synthetic) AS real_active,
       count(DISTINCT unit_of_measure)                 AS distinct_units,
       count(*) FILTER (WHERE expected_min_value IS NOT NULL OR expected_max_value IS NOT NULL) AS with_bounds
FROM public.parameter_definitions;
"@

Show "2. EVERY PHRASE THIS QUESTION MATCHES, REAL AND SYNTHETIC" @"
WITH normalised AS (
  SELECT parameter_code,
         parameter_name,
         value_type,
         unit_of_measure,
         expected_min_value,
         expected_max_value,
         is_synthetic,
         is_deleted,
         regexp_replace(lower(translate(parameter_code, '._-', '   ')), '\s+', ' ', 'g') AS norm_code,
         regexp_replace(lower(translate(parameter_name, '._-', '   ')), '\s+', ' ', 'g') AS norm_name
  FROM public.parameter_definitions
)
SELECT parameter_code,
       parameter_name,
       value_type,
       unit_of_measure   AS unit,
       expected_min_value AS min,
       expected_max_value AS max,
       is_synthetic,
       is_deleted,
       CASE WHEN position(btrim(norm_code) in '$NormalisedQuestion') > 0 THEN length(btrim(norm_code)) ELSE 0 END AS code_hit_len,
       CASE WHEN position(btrim(norm_name) in '$NormalisedQuestion') > 0 THEN length(btrim(norm_name)) ELSE 0 END AS name_hit_len
FROM normalised
WHERE position(btrim(norm_code) in '$NormalisedQuestion') > 0
   OR position(btrim(norm_name) in '$NormalisedQuestion') > 0
ORDER BY GREATEST(
           CASE WHEN position(btrim(norm_code) in '$NormalisedQuestion') > 0 THEN length(btrim(norm_code)) ELSE 0 END,
           CASE WHEN position(btrim(norm_name) in '$NormalisedQuestion') > 0 THEN length(btrim(norm_name)) ELSE 0 END) DESC,
         is_synthetic ASC,
         parameter_code;
"@

Show "3. THE SAME, RESTRICTED TO WHAT THE GUARD MAY USE (not deleted, not synthetic)" @"
WITH normalised AS (
  SELECT parameter_code, parameter_name, value_type, unit_of_measure,
         expected_min_value, expected_max_value,
         regexp_replace(lower(translate(parameter_code, '._-', '   ')), '\s+', ' ', 'g') AS norm_code,
         regexp_replace(lower(translate(parameter_name, '._-', '   ')), '\s+', ' ', 'g') AS norm_name
  FROM public.parameter_definitions
  WHERE is_deleted = false AND is_synthetic = false
)
SELECT parameter_code, parameter_name, value_type,
       unit_of_measure AS unit, expected_min_value AS min, expected_max_value AS max,
       GREATEST(
         CASE WHEN position(btrim(norm_code) in '$NormalisedQuestion') > 0 THEN length(btrim(norm_code)) ELSE 0 END,
         CASE WHEN position(btrim(norm_name) in '$NormalisedQuestion') > 0 THEN length(btrim(norm_name)) ELSE 0 END) AS matched_len
FROM normalised
WHERE position(btrim(norm_code) in '$NormalisedQuestion') > 0
   OR position(btrim(norm_name) in '$NormalisedQuestion') > 0
ORDER BY matched_len DESC, parameter_code;
"@

Show "4. WOULD THE LONGEST MATCH BE UNAMBIGUOUS" @"
WITH normalised AS (
  SELECT parameter_code, unit_of_measure,
         GREATEST(
           CASE WHEN position(btrim(regexp_replace(lower(translate(parameter_code, '._-', '   ')), '\s+', ' ', 'g')) in '$NormalisedQuestion') > 0
                THEN length(btrim(regexp_replace(lower(translate(parameter_code, '._-', '   ')), '\s+', ' ', 'g'))) ELSE 0 END,
           CASE WHEN position(btrim(regexp_replace(lower(translate(parameter_name, '._-', '   ')), '\s+', ' ', 'g')) in '$NormalisedQuestion') > 0
                THEN length(btrim(regexp_replace(lower(translate(parameter_name, '._-', '   ')), '\s+', ' ', 'g'))) ELSE 0 END) AS matched_len
  FROM public.parameter_definitions
  WHERE is_deleted = false AND is_synthetic = false
)
SELECT matched_len,
       count(*)                              AS definitions_at_this_length,
       string_agg(parameter_code, ', ')      AS codes,
       string_agg(DISTINCT coalesce(unit_of_measure, '(none)'), ', ') AS units
FROM normalised
WHERE matched_len > 0
GROUP BY matched_len
ORDER BY matched_len DESC;
"@

Show "5. UNITS AND BOUNDS ACROSS THE REAL ACTIVE REGISTRY" @"
SELECT coalesce(unit_of_measure, '(none)') AS unit,
       count(*)                            AS definitions,
       count(*) FILTER (WHERE expected_min_value IS NOT NULL) AS with_min,
       count(*) FILTER (WHERE expected_max_value IS NOT NULL) AS with_max
FROM public.parameter_definitions
WHERE is_deleted = false AND is_synthetic = false
GROUP BY 1
ORDER BY definitions DESC, unit;
"@

Head "WHAT TO READ"
W "Section 4 is the decision. If the top row shows definitions_at_this_length = 1,"
W "the longest unambiguous match resolves and the guard has its authority."
W ""
W "If it shows more than one at the same length, the question is AMBIGUOUS under"
W "the generic rule and the guard must not guess - which is a valid T-074 outcome"
W "and would be reported as such, not worked around."
W ""
W "Section 2 minus section 3 is the legacy/demo vocabulary. If a synthetic row"
W "matches and a real one does not, that is the case the ruling warns about, and"
W "the guard will still refuse to let the synthetic row win."
exit 0
