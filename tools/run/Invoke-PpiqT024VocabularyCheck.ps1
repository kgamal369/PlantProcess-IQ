#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-024 closure requirement 7 - dependency-check the mixed-industry
    reference vocabulary. REPORT ONLY. Nothing is deleted or updated.

.DESCRIPTION
    Answers, explicitly:
      Q1  which Pharma, Tire and Aluminum reference rows are still present
      Q2  which of those rows are referenced by any dashboard, widget,
          definition, metadata binding or presentation-visible selector
      Q3  which selectors filter by industry_template
      Q4  what industry_template value the existing FlatSteel reference set uses
      Q5  whether setting the Fleet v2 defect and parameter rows from NULL to that
          FlatSteel value would make them visible WITHOUT creating duplicate-key
          or binding conflicts

    Then classifies every non-Fleet-v2 row in the four reference tables as
    KEEP, FILTER FROM PRESENTATION, RETIRE/DEACTIVATE or BLOCKED BY DEPENDENCY.

    Q5 matters before anything is retired. The Fleet v2 defect and parameter rows
    were inserted with industry_template NULL while the legacy rows are tagged by
    industry. If a selector filters on that column, THE SAME FILTER THAT HIDES
    PHARMA AND TYRE WOULD ALSO HIDE FLEET V2 - so the tag has to be corrected
    first, not after.

    The reference search is DYNAMIC: it reads every text, varchar and json column
    of every dashboard, widget, page, definition, binding, mapping and kpi table,
    and searches each for each legacy code. Nothing is assumed about which column
    holds a widget's configuration.

.EXAMPLE
    .\tools\run\Invoke-PpiqT024VocabularyCheck.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$OutDir     = "docs\m1\evidence",
    [string]$PsqlPath   = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

function Write-Head {
    param([string]$Text)
    Write-Host ""
    Write-Host ("=" * 78)
    Write-Host $Text
    Write-Host ("=" * 78)
}
function Count-NonAscii {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return 0 }
    $n = 0
    foreach ($ch in $Text.ToCharArray()) { if ([int]$ch -gt 126) { $n = $n + 1 } }
    return $n
}
function Resolve-Psql {
    param([string]$Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        if (Test-Path -LiteralPath $Explicit) { return $Explicit }
        return $null
    }
    $c = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($null -ne $c) { return $c.Source }
    foreach ($p in @("C:\Program Files\PostgreSQL\16\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\17\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\15\bin\psql.exe")) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
}

Write-Head "PPIQ T-024 REQUIREMENT 7 - VOCABULARY DEPENDENCY CHECK (REPORT ONLY)"
$repoRoot = (Get-Location).Path
$psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
Write-Host ("Database : " + $Database + "   READ ONLY - nothing is deleted or updated")
Write-Host ("psql     : " + $psql)

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outFolder = $OutDir
if (-not [System.IO.Path]::IsPathRooted($OutDir)) { $outFolder = Join-Path $repoRoot $OutDir }
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$evidencePath = Join-Path $outFolder ("T-024_vocabulary_dependency_" + $stamp + ".txt")
$tmpDir = Join-Path $env:TEMP ("ppiq_t024voc_" + $stamp)
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
$sqlFile = Join-Path $tmpDir "vocab.sql"
$resFile = Join-Path $tmpDir "result.txt"
$errFile = Join-Path $tmpDir "stderr.txt"

$sql = @'
\pset pager off
\pset border 2
\timing off

-- ================================================================
-- the Fleet v2 vocabulary, declared once and reused by every section
-- ================================================================
DROP TABLE IF EXISTS fleet_defect, fleet_param, fleet_equip, fleet_mut,
                     needles, hits;
CREATE TEMP TABLE fleet_defect(code text);
INSERT INTO fleet_defect VALUES
 ('SCALE'),('EDGE_CRACK'),('ROLLED_IN_SCALE'),('SLIVER'),('INCLUSION'),('PINHOLE'),
 ('SCRATCH'),('WAVINESS'),('CENTRE_BUCKLE'),('EDGE_WAVE'),('ROLL_MARK'),
 ('LAMINATION'),('OIL_SPOT'),('SENSOR_ARTEFACT');

CREATE TEMP TABLE fleet_param(code text);
INSERT INTO fleet_param VALUES
 ('CARBON_PCT'),('MANGANESE_PCT'),('SILICON_PCT'),('SULPHUR_PCT'),('PHOSPHORUS_PCT'),
 ('ALUMINIUM_PCT'),('TAP_TEMP_C'),('OXYGEN_NM3'),('POWER_KWH'),('LF_ARGON_NM3'),
 ('LF_CALCIUM_M'),('LF_FINAL_TEMP_C'),('CASTING_SPEED_MPM'),('SUPERHEAT_C'),
 ('MOULD_LEVEL_AVG'),('FDT_C'),('CT_C'),('THICKNESS_MM'),('WIDTH_MM'),
 ('ROLL_FORCE_KN'),('ROLL_GAP_MM'),('ROLL_SPEED_MPS'),('ROLL_TEMP_C'),
 ('ACID_CONC_PCT'),('BATH_TEMP_C'),('LINE_SPEED_MPM'),('QA_WIDTH_MM'),('QA_THK_MM'),
 ('QA_ROUGHNESS_UM');

CREATE TEMP TABLE fleet_equip(code text);
INSERT INTO fleet_equip VALUES
 ('EAF-01'),('EAF-02'),('LF-01'),('LF-02'),('CCM-01'),('CCM-02'),('HSM-01'),
 ('PKL-01'),('PKL-02'),('PARSYTEC-01'),('PARSYTEC-02'),('HSM-01-F1'),('HSM-01-F2'),
 ('HSM-01-F3'),('HSM-01-F4'),('HSM-01-F5'),('HSM-01-F6'),('HSM-01-F7');

CREATE TEMP TABLE fleet_mut(code text);
INSERT INTO fleet_mut VALUES ('Heat'),('Cast'),('Slab'),('Coil');

\qecho
\qecho --- scaffolding check: these four MUST be non-zero ---
SELECT (SELECT count(*) FROM fleet_defect) AS fleet_defect,
       (SELECT count(*) FROM fleet_param)  AS fleet_param,
       (SELECT count(*) FROM fleet_equip)  AS fleet_equip,
       (SELECT count(*) FROM fleet_mut)    AS fleet_mut;

\qecho
\qecho ================================================================
\qecho Q1 - WHICH NON-FLEET-V2 REFERENCE ROWS ARE STILL PRESENT
\qecho ================================================================
\qecho --- defect_catalogs ---
SELECT defect_code, defect_name, coalesce(defect_category,'(null)') AS category,
       coalesce(industry_template,'(null)') AS industry_template,
       coalesce(source_system,'(null)') AS source_system
FROM public.defect_catalogs
WHERE defect_code NOT IN (SELECT code FROM fleet_defect)
ORDER BY 4, 1;

\qecho --- parameter_definitions ---
SELECT parameter_code, parameter_name,
       coalesce(unit_of_measure,'(null)') AS unit,
       coalesce(industry_template,'(null)') AS industry_template,
       coalesce(source_system,'(null)') AS source_system
FROM public.parameter_definitions
WHERE parameter_code NOT IN (SELECT code FROM fleet_param)
ORDER BY 4, 1;

\qecho --- equipment ---
SELECT equipment_code, equipment_name, equipment_type, is_active,
       coalesce(source_system,'(null)') AS source_system
FROM public.equipment
WHERE equipment_code NOT IN (SELECT code FROM fleet_equip)
ORDER BY 1;

\qecho --- material_unit_type_definitions ---
SELECT m.material_unit_type_code, m.material_unit_type_name, m.is_active,
       coalesce(t.template_code,'(null)') AS industry_template,
       coalesce(m.source_system,'(null)') AS source_system
FROM public.material_unit_type_definitions m
LEFT JOIN public.industry_templates t ON t.id = m.industry_template_id
WHERE m.material_unit_type_code NOT IN (SELECT code FROM fleet_mut)
ORDER BY 4, 1;

\qecho
\qecho ================================================================
\qecho Q4 - WHAT industry_template VALUE DOES THE FLAT STEEL SET USE
\qecho ================================================================
\qecho --- industry_templates, all of them ---
SELECT template_code, template_name, industry_name, is_active
FROM public.industry_templates ORDER BY 1;

\qecho --- industry_template as it appears on the reference tables ---
SELECT 'defect_catalogs' AS table_name,
       coalesce(industry_template,'(null)') AS industry_template, count(*) AS rows
FROM public.defect_catalogs GROUP BY 2
UNION ALL
SELECT 'parameter_definitions', coalesce(industry_template,'(null)'), count(*)
FROM public.parameter_definitions GROUP BY 2
ORDER BY 1, 2;

\qecho
\qecho ================================================================
\qecho Q3 - WHICH SELECTORS FILTER BY industry_template
\qecho views, matviews and functions whose body mentions the column
\qecho ================================================================
SELECT c.relname AS object_name,
       CASE c.relkind WHEN 'v' THEN 'view' WHEN 'm' THEN 'matview' END AS kind
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = 'public'
WHERE c.relkind IN ('v','m')
  AND pg_get_viewdef(c.oid) ILIKE '%industry_template%'
ORDER BY 1;

-- prokind = 'f' only: pg_get_functiondef() THROWS on an aggregate, and
-- array_agg is one, which killed this statement outright.
SELECT p.proname AS function_name
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname IN ('public','ppiq_forensics')
  AND p.prokind = 'f'
  AND pg_get_functiondef(p.oid) ILIKE '%industry_template%'
ORDER BY 1;

\qecho
\qecho ================================================================
\qecho Q2 - WHICH LEGACY CODES ARE REFERENCED ANYWHERE
\qecho Every text, varchar and json column of every dashboard, widget,
\qecho page, definition, binding, mapping and kpi table is searched.
\qecho Nothing is assumed about which column holds a configuration.
\qecho ================================================================
CREATE TEMP TABLE needles(kind text, code text);
INSERT INTO needles
SELECT 'defect', defect_code FROM public.defect_catalogs
WHERE defect_code NOT IN (SELECT code FROM fleet_defect)
UNION ALL
SELECT 'parameter', parameter_code FROM public.parameter_definitions
WHERE parameter_code NOT IN (SELECT code FROM fleet_param)
UNION ALL
SELECT 'equipment', equipment_code FROM public.equipment
WHERE equipment_code NOT IN (SELECT code FROM fleet_equip)
UNION ALL
SELECT 'material_unit_type', material_unit_type_code
FROM public.material_unit_type_definitions
WHERE material_unit_type_code NOT IN (SELECT code FROM fleet_mut);

CREATE TEMP TABLE hits(table_name text, column_name text, kind text, code text,
                       matches bigint);
DO $vocab$
DECLARE r record; nd record; c bigint;
BEGIN
  FOR r IN
    SELECT col.table_name, col.column_name
    FROM information_schema.columns col
    JOIN pg_class pc ON pc.relname = col.table_name
    JOIN pg_namespace pn ON pn.oid = pc.relnamespace AND pn.nspname = 'public'
    WHERE col.table_schema = 'public' AND pc.relkind = 'r'
      AND col.data_type IN ('text','character varying','jsonb','json')
      AND (col.table_name LIKE '%dashboard%' OR col.table_name LIKE '%widget%'
        OR col.table_name LIKE '%page%'      OR col.table_name LIKE '%definition%'
        OR col.table_name LIKE '%binding%'   OR col.table_name LIKE '%mapping%'
        OR col.table_name LIKE '%kpi%'       OR col.table_name LIKE '%layout%')
      AND col.table_name NOT IN ('defect_catalogs','parameter_definitions',
                                 'material_unit_type_definitions',
                                 'operation_definitions','source_field_definitions')
  LOOP
    FOR nd IN SELECT kind, code FROM needles LOOP
      EXECUTE format('SELECT count(*) FROM public.%I WHERE %I::text LIKE %L',
                     r.table_name, r.column_name, '%' || nd.code || '%')
      INTO c;
      IF c > 0 THEN
        INSERT INTO hits VALUES (r.table_name, r.column_name, nd.kind, nd.code, c);
      END IF;
    END LOOP;
  END LOOP;
END
$vocab$;

\qecho --- columns actually searched ---
SELECT col.table_name, count(*) AS columns_searched
FROM information_schema.columns col
JOIN pg_class pc ON pc.relname = col.table_name
JOIN pg_namespace pn ON pn.oid = pc.relnamespace AND pn.nspname = 'public'
WHERE col.table_schema = 'public' AND pc.relkind = 'r'
  AND col.data_type IN ('text','character varying','jsonb','json')
  AND (col.table_name LIKE '%dashboard%' OR col.table_name LIKE '%widget%'
    OR col.table_name LIKE '%page%'      OR col.table_name LIKE '%definition%'
    OR col.table_name LIKE '%binding%'   OR col.table_name LIKE '%mapping%'
    OR col.table_name LIKE '%kpi%'       OR col.table_name LIKE '%layout%')
  AND col.table_name NOT IN ('defect_catalogs','parameter_definitions',
                             'material_unit_type_definitions',
                             'operation_definitions','source_field_definitions')
GROUP BY 1 ORDER BY 1;

\qecho --- every hit ---
SELECT kind, code, table_name, column_name, matches
FROM hits ORDER BY 1, 2, 3, 4;

\qecho --- hits summarised per code ---
SELECT n.kind, n.code, coalesce(sum(h.matches),0) AS total_references
FROM needles n LEFT JOIN hits h ON h.kind = n.kind AND h.code = n.code
GROUP BY 1,2 ORDER BY 3 DESC, 1, 2;

\qecho
\qecho ================================================================
\qecho Q2b - LEGACY ROWS REFERENCED BY OPERATIONAL DATA
\qecho after the replacement this should be empty, since every
\qecho operational row is Fleet v2
\qecho ================================================================
SELECT 'defect_catalogs used by a quality event' AS check_name, count(*) AS rows
FROM public.quality_events q JOIN public.defect_catalogs d ON d.id = q.defect_catalog_id
WHERE d.defect_code NOT IN (SELECT code FROM fleet_defect)
UNION ALL
SELECT 'parameter_definitions used by an observation', count(*)
FROM public.parameter_observations o
JOIN public.parameter_definitions p ON p.id = o.parameter_definition_id
WHERE p.parameter_code NOT IN (SELECT code FROM fleet_param)
UNION ALL
SELECT 'equipment used by a step or observation or downtime', count(*)
FROM public.equipment e
WHERE e.equipment_code NOT IN (SELECT code FROM fleet_equip)
  AND (EXISTS (SELECT 1 FROM public.process_step_executions s WHERE s.equipment_id = e.id)
    OR EXISTS (SELECT 1 FROM public.parameter_observations o WHERE o.equipment_id = e.id)
    OR EXISTS (SELECT 1 FROM public.downtime_events d WHERE d.equipment_id = e.id))
UNION ALL
SELECT 'material unit types used by a unit', count(*)
FROM public.material_unit_type_definitions m
WHERE m.material_unit_type_code NOT IN (SELECT code FROM fleet_mut)
  AND EXISTS (SELECT 1 FROM public.material_units u
               WHERE u.material_unit_type = m.material_unit_type_code);

\qecho
\qecho ================================================================
\qecho Q5 - WOULD TAGGING FLEET V2 AS FLAT STEEL CREATE A CONFLICT
\qecho parameter_definitions is UNIQUE on (industry_template, parameter_code)
\qecho and defect_catalogs is UNIQUE on defect_code alone
\qecho ================================================================
\qecho --- fleet v2 rows currently carrying a NULL industry_template ---
SELECT 'defect_catalogs' AS table_name, count(*) AS fleet_v2_rows_with_null_template
FROM public.defect_catalogs
WHERE defect_code IN (SELECT code FROM fleet_defect) AND industry_template IS NULL
UNION ALL
SELECT 'parameter_definitions', count(*)
FROM public.parameter_definitions
WHERE parameter_code IN (SELECT code FROM fleet_param) AND industry_template IS NULL;

\qecho --- collisions IF the fleet v2 rows were tagged with each candidate value ---
SELECT t.industry_template AS candidate_value,
       (SELECT count(*) FROM public.parameter_definitions a
         WHERE a.parameter_code IN (SELECT code FROM fleet_param)
           AND a.industry_template IS NULL
           AND EXISTS (SELECT 1 FROM public.parameter_definitions b
                        WHERE b.parameter_code = a.parameter_code
                          AND b.industry_template IS NOT DISTINCT FROM t.industry_template))
         AS parameter_collisions
FROM (SELECT DISTINCT industry_template FROM public.parameter_definitions
      WHERE industry_template IS NOT NULL) t
ORDER BY 1;

\qecho
\qecho ================================================================
\qecho CLASSIFICATION - EVERY NON-FLEET-V2 REFERENCE ROW
\qecho KEEP                    steel vocabulary that belongs to this plant
\qecho FILTER FROM PRESENTATION referenced somewhere, so it cannot be removed
\qecho RETIRE/DEACTIVATE       not steel and referenced nowhere
\qecho BLOCKED BY DEPENDENCY   still used by operational or definition data
\qecho ================================================================
SELECT 'defect_catalogs' AS reference_table, d.defect_code AS code,
       coalesce(d.industry_template,'(null)') AS industry_template,
       coalesce((SELECT sum(matches) FROM hits h
                  WHERE h.kind='defect' AND h.code = d.defect_code),0) AS refs,
       CASE
         WHEN EXISTS (SELECT 1 FROM public.quality_events q WHERE q.defect_catalog_id = d.id)
           THEN 'BLOCKED BY DEPENDENCY'
         WHEN coalesce((SELECT sum(matches) FROM hits h
                         WHERE h.kind='defect' AND h.code = d.defect_code),0) > 0
           THEN 'FILTER FROM PRESENTATION'
         WHEN d.industry_template ILIKE '%steel%' THEN 'KEEP'
         ELSE 'RETIRE/DEACTIVATE'
       END AS classification
FROM public.defect_catalogs d
WHERE d.defect_code NOT IN (SELECT code FROM fleet_defect)
UNION ALL
SELECT 'parameter_definitions', p.parameter_code,
       coalesce(p.industry_template,'(null)'),
       coalesce((SELECT sum(matches) FROM hits h
                  WHERE h.kind='parameter' AND h.code = p.parameter_code),0),
       CASE
         WHEN EXISTS (SELECT 1 FROM public.parameter_observations o
                       WHERE o.parameter_definition_id = p.id)
           THEN 'BLOCKED BY DEPENDENCY'
         WHEN coalesce((SELECT sum(matches) FROM hits h
                         WHERE h.kind='parameter' AND h.code = p.parameter_code),0) > 0
           THEN 'FILTER FROM PRESENTATION'
         WHEN p.industry_template ILIKE '%steel%' THEN 'KEEP'
         ELSE 'RETIRE/DEACTIVATE'
       END
FROM public.parameter_definitions p
WHERE p.parameter_code NOT IN (SELECT code FROM fleet_param)
UNION ALL
SELECT 'equipment', e.equipment_code, '(n/a)',
       coalesce((SELECT sum(matches) FROM hits h
                  WHERE h.kind='equipment' AND h.code = e.equipment_code),0),
       CASE
         WHEN EXISTS (SELECT 1 FROM public.process_step_executions s WHERE s.equipment_id = e.id)
           OR EXISTS (SELECT 1 FROM public.parameter_observations o WHERE o.equipment_id = e.id)
           OR EXISTS (SELECT 1 FROM public.downtime_events d WHERE d.equipment_id = e.id)
           OR EXISTS (SELECT 1 FROM public.equipment c WHERE c.parent_equipment_id = e.id)
           THEN 'BLOCKED BY DEPENDENCY'
         WHEN coalesce((SELECT sum(matches) FROM hits h
                         WHERE h.kind='equipment' AND h.code = e.equipment_code),0) > 0
           THEN 'FILTER FROM PRESENTATION'
         ELSE 'RETIRE/DEACTIVATE'
       END
FROM public.equipment e
WHERE e.equipment_code NOT IN (SELECT code FROM fleet_equip)
UNION ALL
SELECT 'material_unit_type_definitions', m.material_unit_type_code,
       coalesce(t.template_code,'(null)'),
       coalesce((SELECT sum(matches) FROM hits h
                  WHERE h.kind='material_unit_type' AND h.code = m.material_unit_type_code),0),
       CASE
         WHEN EXISTS (SELECT 1 FROM public.material_units u
                       WHERE u.material_unit_type = m.material_unit_type_code)
           THEN 'BLOCKED BY DEPENDENCY'
         WHEN coalesce((SELECT sum(matches) FROM hits h
                         WHERE h.kind='material_unit_type' AND h.code = m.material_unit_type_code),0) > 0
           THEN 'FILTER FROM PRESENTATION'
         ELSE 'RETIRE/DEACTIVATE'
       END
FROM public.material_unit_type_definitions m
LEFT JOIN public.industry_templates t ON t.id = m.industry_template_id
WHERE m.material_unit_type_code NOT IN (SELECT code FROM fleet_mut)
ORDER BY 1, 5, 2;

\qecho
\qecho --- classification totals ---
\qecho (recomputed independently of the listing above)
SELECT classification, count(*) AS rows FROM (
  SELECT CASE
      WHEN EXISTS (SELECT 1 FROM public.quality_events q WHERE q.defect_catalog_id = d.id)
        THEN 'BLOCKED BY DEPENDENCY'
      WHEN coalesce((SELECT sum(matches) FROM hits h
                      WHERE h.kind='defect' AND h.code = d.defect_code),0) > 0
        THEN 'FILTER FROM PRESENTATION'
      WHEN d.industry_template ILIKE '%steel%' THEN 'KEEP'
      ELSE 'RETIRE/DEACTIVATE' END AS classification
  FROM public.defect_catalogs d
  WHERE d.defect_code NOT IN (SELECT code FROM fleet_defect)
  UNION ALL
  SELECT CASE
      WHEN EXISTS (SELECT 1 FROM public.parameter_observations o
                    WHERE o.parameter_definition_id = p.id)
        THEN 'BLOCKED BY DEPENDENCY'
      WHEN coalesce((SELECT sum(matches) FROM hits h
                      WHERE h.kind='parameter' AND h.code = p.parameter_code),0) > 0
        THEN 'FILTER FROM PRESENTATION'
      WHEN p.industry_template ILIKE '%steel%' THEN 'KEEP'
      ELSE 'RETIRE/DEACTIVATE' END
  FROM public.parameter_definitions p
  WHERE p.parameter_code NOT IN (SELECT code FROM fleet_param)
) z GROUP BY 1 ORDER BY 2 DESC;

\qecho
\qecho ================================================================
\qecho END - NOTHING WAS DELETED OR UPDATED
\qecho ================================================================
'@

[System.IO.File]::WriteAllText($sqlFile, $sql, (New-Object System.Text.UTF8Encoding($false)))

$prevC = [Console]::OutputEncoding
$prevO = $OutputEncoding
$exit = 1
try {
    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $OutputEncoding           = New-Object System.Text.UTF8Encoding($false)
    $env:PGPASSWORD           = $PgPassword
    $env:PGCLIENTENCODING     = "UTF8"
    Write-Head "RUNNING"
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
           "-v", "ON_ERROR_STOP=0", "-f", $sqlFile, "-o", $resFile)
    $p = Start-Process -FilePath $psql -ArgumentList $a -NoNewWindow -Wait -PassThru `
                       -RedirectStandardError $errFile
    $exit = $p.ExitCode
    Write-Host ("psql exit : " + $exit)
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    [Console]::OutputEncoding = $prevC
    $OutputEncoding           = $prevO
}

$errText = ""
if (Test-Path -LiteralPath $errFile) { $errText = [System.IO.File]::ReadAllText($errFile) }
if (-not [string]::IsNullOrWhiteSpace($errText)) {
    Write-Head "PSQL STDERR"
    Write-Host $errText
}
if (-not (Test-Path -LiteralPath $resFile)) { Write-Host "[FAIL] no result."; exit 3 }
$result = [System.IO.File]::ReadAllText($resFile)
if ([string]::IsNullOrWhiteSpace($result)) { Write-Host "[FAIL] empty result."; exit 3 }

$result = $result -replace "`r`n", "`n"
$clean = New-Object System.Text.StringBuilder
foreach ($ch in $result.ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
$result = $clean.ToString()

$header = @(
    "================================================================",
    "PPIQ T-024 REQUIREMENT 7 - VOCABULARY DEPENDENCY CHECK",
    "================================================================",
    ("Generated At : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")),
    ("Database     : " + $Database),
    ("psql exit    : " + $exit),
    "",
    "REPORT ONLY. Nothing was deleted or updated.",
    "",
    "Q5 IS THE ONE TO READ FIRST. The Fleet v2 defect and parameter rows",
    "carry industry_template NULL while the legacy rows are tagged by",
    "industry. If a selector filters on that column, the same filter that",
    "hides Pharma and Tire would also hide Fleet v2 - so the tag must be",
    "corrected BEFORE any legacy vocabulary is retired, not after.",
    "================================================================",
    ""
) -join "`r`n"

$final = $header + "`r`n" + ($result -replace "`n", "`r`n")
[System.IO.File]::WriteAllText($evidencePath, $final, (New-Object System.Text.UTF8Encoding($false)))

$len = 0
if (Test-Path -LiteralPath $evidencePath) { $len = (Get-Item -LiteralPath $evidencePath).Length }
$nonAscii = Count-NonAscii ([System.IO.File]::ReadAllText($evidencePath))

Write-Head "RESULT"
Write-Host ("Evidence  : " + $evidencePath)
Write-Host ("Bytes     : " + $len)
Write-Host ("Non-ASCII : " + $nonAscii)
if ($len -lt 1024) { Write-Host "[FAIL] evidence under 1 KB."; exit 5 }
if ($nonAscii -gt 0) { Write-Host "[FAIL] non-ASCII."; exit 4 }
Write-Host ""
Write-Host "[OK] Reported. NOTHING was deleted or updated."
exit 0
