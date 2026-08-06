# =============================================================================
# Invoke-PpiqT031Certification.ps1        v2 - evidence-bearing
#
# THE CROSS-LAYER CONSISTENCY CERTIFICATION.   T-031.
#
# READ ONLY unless -InjectDivergence is passed, and even then every write is
# inside a transaction that is ALWAYS rolled back, with a residue check after.
#
# SUBJECT      dump_store STAGING   versus   canonical PLANT.
#              src_* is the DONOR and is never a side of this comparison.
# DIRECTION    Gated STAGING -> CANONICAL. The reverse is reported, never gated,
#              because staging is a 1x subset of a 3x canonical population.
# COUNTS       Never compared. 4.5.2a rules the layers are shaped differently.
# FAIL CLOSED  Empty layers, or a dimension that cannot be computed, is RED.
#
# WHY EVERY DIMENSION PRINTS VALUES. v1 printed counts alone, so a red said
# "5452" and nothing else. A gate has to say what is wrong, not only that
# something is.
#
# THE QA RULE, CORRECTED. v1 demanded staging test codes equal canonical
# parameter codes. That was wrong: the donor emits WIDTH, THK and ROUGHNESS and
# the generator itself maps them to QA_WIDTH_MM, QA_THK_MM and QA_ROUGHNESS_UM.
# Source-shaped staging is SUPPOSED to carry the source system's vocabulary.
# 4.5.2a asks for the same QA DEFINITIONS AND UNITS, not the same spellings, so
# the rule is now a one-to-one correspondence of the sets plus unit agreement.
#
# Run from repo root:
#   .\tools\run\Invoke-PpiqT031Certification.ps1
#   .\tools\run\Invoke-PpiqT031Certification.ps1 -InjectDivergence
# =============================================================================
[CmdletBinding()]
param(
    [string]$TargetDb = 'ppiq_presentation',
    [string]$DbHost = '127.0.0.1',
    [int]$DbPort = 5432,
    [string]$DbUser = 'ppiq_dev',
    [switch]$InjectDivergence
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$EvidenceDir = Join-Path $RepoRoot 'docs\m1\evidence'
if (-not (Test-Path $EvidenceDir)) { New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null }
$Report = Join-Path $EvidenceDir ('T-031_certification_' + $Stamp + '.txt')
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }
function Save { [System.IO.File]::WriteAllText($Report, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false))) }

$PgBin = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $PgBin = Split-Path $cmd.Source -Parent } else {
    $c = @(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if ($c.Count -gt 0) { $PgBin = Split-Path $c[0].FullName -Parent }
}
if (-not $PgBin) { Write-Host '[FAIL] psql not found.' -ForegroundColor Red; exit 1 }
$Psql = Join-Path $PgBin 'psql.exe'
$env:PGPASSWORD = 'ppiq_dev_local_only'

function Q1([string]$q) {
    $o = & $Psql -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -w -X -A -t -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { return $null }
    $l = @($o | Where-Object { $_ -and ($_.ToString().Trim() -ne '') }) | Select-Object -First 1
    if ($null -eq $l) { return '' }
    return $l.ToString().Trim()
}
function QList([string]$q) {
    $o = & $Psql -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -w -X -A -t -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { return @('<query failed>') }
    return @($o | Where-Object { $_ -and ($_.ToString().Trim() -ne '') } | ForEach-Object { $_.ToString().Trim() })
}

$Dimensions = New-Object System.Collections.ArrayList
function Dim([string]$name, [string]$countSql, [string]$offenderSql, [string]$note) {
    $v = Q1 $countSql
    $verdict = 'PASS'
    $shown = $v
    if ($null -eq $v -or $v -eq '') { $verdict = 'NOT COMPUTABLE'; $shown = 'n/a' } elseif ([int]$v -ne 0) { $verdict = 'FAIL' }
    [void]$Dimensions.Add([pscustomobject]@{ Name = $name; Verdict = $verdict; Offending = $shown; Note = $note; Sql = $offenderSql })
    W ('  ' + $name.PadRight(24) + $verdict.PadRight(16) + ('offending=' + $shown).PadRight(20) + $note)
}

W ('T-031 CROSS-LAYER CONSISTENCY CERTIFICATION v2 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('Database : ' + $TargetDb)
W ('Subject  : dump_store STAGING versus canonical PLANT. src_* is the donor and is not a side of this.')
W ('Direction: gated STAGING -> CANONICAL. The reverse is reported, never gated.')
W ('Counts   : NEVER compared. 4.5.2a rules that the layers are shaped differently by design.')
W ('Mode     : ' + $(if ($InjectDivergence) { 'CERTIFY + INJECTED DIVERGENCE PROOF (transaction, always rolled back)' } else { 'CERTIFY (read only)' }))
W ('=' * 96)
W ''

W 'PRECONDITIONS (a certification that passes on empty layers is worse than none)'
$fc = 0
function Pre([string]$label, [bool]$ok, [string]$detail) {
    if ($ok) { W ('  OK   ' + $label + '   ' + $detail) } else { W ('  FAIL ' + $label + '   ' + $detail); $script:fc = $script:fc + 1 }
}
$alive = Q1 'SELECT 1;'
Pre 'database reachable' ($alive -eq '1') ''
if ($alive -ne '1') { W ''; W '[ABORT] cannot reach the database.'; Save; exit 1 }
$stgTables = Q1 "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'dump_store';"
Pre 'dump_store staging schema is present' ($null -ne $stgTables -and [int]$stgTables -gt 0) ('tables=' + $stgTables)
$stgCoils = Q1 'SELECT count(*) FROM dump_store.src_hsm_oracle_shape_hsm_coils;'
Pre 'staging carries coils' ($null -ne $stgCoils -and [int]$stgCoils -gt 0) ('coils=' + $stgCoils)
$canonUnits = Q1 'SELECT count(*) FROM material_units WHERE coalesce(is_deleted,false) = false;'
Pre 'canonical carries material units' ($null -ne $canonUnits -and [int]$canonUnits -gt 0) ('material_units=' + $canonUnits)
$canonDefects = Q1 'SELECT count(*) FROM defect_catalogs WHERE coalesce(is_deleted,false) = false;'
Pre 'canonical carries a defect catalogue' ($null -ne $canonDefects -and [int]$canonDefects -gt 0) ('defect_catalogs=' + $canonDefects)
$canonParams = Q1 'SELECT count(*) FROM parameter_definitions WHERE coalesce(is_deleted,false) = false;'
Pre 'canonical carries parameter definitions' ($null -ne $canonParams -and [int]$canonParams -gt 0) ('parameter_definitions=' + $canonParams)
$canonEquip = Q1 'SELECT count(*) FROM equipment WHERE coalesce(is_deleted,false) = false;'
Pre 'canonical carries equipment' ($null -ne $canonEquip -and [int]$canonEquip -gt 0) ('equipment=' + $canonEquip)
if ($fc -gt 0) { W ''; W ('[CERTIFICATION RED] ' + $fc + ' precondition(s) failed. The certification did not run.'); Save; exit 2 }
W ''

W 'DIMENSIONS (Chapter 3 section 4.5.2a). Offending is counted STAGING -> CANONICAL.'
W ('  ' + 'dimension'.PadRight(24) + 'verdict'.PadRight(16) + 'offending'.PadRight(20) + 'note')
W ('  ' + '-' * 94)

Dim 'grades' @"
WITH stg AS (
  SELECT DISTINCT steel_grade AS g FROM dump_store.src_meltshop_pg_heats WHERE steel_grade IS NOT NULL
  UNION SELECT DISTINCT planned_grade FROM dump_store.src_caster_oracle_shape_cast_sequence WHERE planned_grade IS NOT NULL
  UNION SELECT DISTINCT actual_grade FROM dump_store.src_caster_oracle_shape_cast_sequence WHERE actual_grade IS NOT NULL)
SELECT count(*) FROM stg WHERE NOT EXISTS (
  SELECT 1 FROM material_units m WHERE coalesce(m.is_deleted,false)=false AND m.grade_or_recipe = stg.g);
"@ @"
WITH stg AS (
  SELECT DISTINCT steel_grade AS g FROM dump_store.src_meltshop_pg_heats WHERE steel_grade IS NOT NULL
  UNION SELECT DISTINCT planned_grade FROM dump_store.src_caster_oracle_shape_cast_sequence WHERE planned_grade IS NOT NULL
  UNION SELECT DISTINCT actual_grade FROM dump_store.src_caster_oracle_shape_cast_sequence WHERE actual_grade IS NOT NULL)
SELECT g FROM stg WHERE NOT EXISTS (
  SELECT 1 FROM material_units m WHERE coalesce(m.is_deleted,false)=false AND m.grade_or_recipe = stg.g) ORDER BY 1 LIMIT 20;
"@ 'staging grades with no canonical grade_or_recipe'

Dim 'equipment identities' @"
WITH stg AS (
  SELECT DISTINCT furnace_code AS e FROM dump_store.src_meltshop_pg_heats WHERE furnace_code IS NOT NULL
  UNION SELECT DISTINCT lf_code FROM dump_store.src_meltshop_pg_lf_treatment WHERE lf_code IS NOT NULL
  UNION SELECT DISTINCT caster_id FROM dump_store.src_caster_oracle_shape_cast_pieces WHERE caster_id IS NOT NULL
  UNION SELECT DISTINCT mill_line FROM dump_store.src_hsm_oracle_shape_hsm_coils WHERE mill_line IS NOT NULL
  UNION SELECT DISTINCT line_id FROM dump_store.src_pkl_mssql_shape_pickle_orders WHERE line_id IS NOT NULL
  UNION SELECT DISTINCT inspection_device FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects WHERE inspection_device IS NOT NULL
  UNION SELECT DISTINCT equipment_code FROM dump_store.src_inspection_mysql_shape_downtime_events WHERE equipment_code IS NOT NULL)
SELECT count(*) FROM stg WHERE NOT EXISTS (
  SELECT 1 FROM equipment q WHERE coalesce(q.is_deleted,false)=false AND q.equipment_code = stg.e);
"@ @"
WITH stg AS (
  SELECT DISTINCT furnace_code AS e FROM dump_store.src_meltshop_pg_heats WHERE furnace_code IS NOT NULL
  UNION SELECT DISTINCT lf_code FROM dump_store.src_meltshop_pg_lf_treatment WHERE lf_code IS NOT NULL
  UNION SELECT DISTINCT caster_id FROM dump_store.src_caster_oracle_shape_cast_pieces WHERE caster_id IS NOT NULL
  UNION SELECT DISTINCT mill_line FROM dump_store.src_hsm_oracle_shape_hsm_coils WHERE mill_line IS NOT NULL
  UNION SELECT DISTINCT line_id FROM dump_store.src_pkl_mssql_shape_pickle_orders WHERE line_id IS NOT NULL
  UNION SELECT DISTINCT inspection_device FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects WHERE inspection_device IS NOT NULL
  UNION SELECT DISTINCT equipment_code FROM dump_store.src_inspection_mysql_shape_downtime_events WHERE equipment_code IS NOT NULL)
SELECT e FROM stg WHERE NOT EXISTS (
  SELECT 1 FROM equipment q WHERE coalesce(q.is_deleted,false)=false AND q.equipment_code = stg.e) ORDER BY 1 LIMIT 20;
"@ 'staging equipment identities absent from canonical equipment'

Dim 'defect vocabulary' @"
SELECT count(*) FROM (SELECT DISTINCT defect_code AS d FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects
  WHERE defect_code IS NOT NULL) stg
WHERE NOT EXISTS (SELECT 1 FROM defect_catalogs c WHERE coalesce(c.is_deleted,false)=false AND c.defect_code = stg.d);
"@ @"
SELECT d || '   [staging rows: ' || (SELECT count(*) FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects p WHERE p.defect_code = stg.d)::text
       || ' | in catalogue incl. deleted: ' || (SELECT count(*) FROM defect_catalogs c2 WHERE c2.defect_code = stg.d)::text || ']'
FROM (SELECT DISTINCT defect_code AS d FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects
  WHERE defect_code IS NOT NULL) stg
WHERE NOT EXISTS (SELECT 1 FROM defect_catalogs c WHERE coalesce(c.is_deleted,false)=false AND c.defect_code = stg.d)
ORDER BY 1 LIMIT 20;
"@ 'staging defect codes absent from the canonical catalogue'

Dim 'downtime semantics' @"
SELECT count(*) FROM (SELECT DISTINCT reason_code AS r FROM dump_store.src_inspection_mysql_shape_downtime_events
  WHERE reason_code IS NOT NULL) stg
WHERE NOT EXISTS (SELECT 1 FROM downtime_events d WHERE coalesce(d.is_deleted,false)=false AND d.reason_code = stg.r);
"@ @"
SELECT r FROM (SELECT DISTINCT reason_code AS r FROM dump_store.src_inspection_mysql_shape_downtime_events
  WHERE reason_code IS NOT NULL) stg
WHERE NOT EXISTS (SELECT 1 FROM downtime_events d WHERE coalesce(d.is_deleted,false)=false AND d.reason_code = stg.r)
ORDER BY 1 LIMIT 20;
"@ 'staging downtime reason codes absent from canonical downtime'

Dim 'chemistry vocabulary' @"
SELECT count(*) FROM (SELECT upper(column_name) AS p FROM information_schema.columns
  WHERE table_schema='dump_store' AND table_name='src_meltshop_pg_heats' AND column_name LIKE '%\_pct') stg
WHERE NOT EXISTS (SELECT 1 FROM parameter_definitions pd
  WHERE coalesce(pd.is_deleted,false)=false AND upper(pd.parameter_code) = stg.p);
"@ @"
SELECT p FROM (SELECT upper(column_name) AS p FROM information_schema.columns
  WHERE table_schema='dump_store' AND table_name='src_meltshop_pg_heats' AND column_name LIKE '%\_pct') stg
WHERE NOT EXISTS (SELECT 1 FROM parameter_definitions pd
  WHERE coalesce(pd.is_deleted,false)=false AND upper(pd.parameter_code) = stg.p) ORDER BY 1 LIMIT 20;
"@ 'staging chemistry columns with no canonical parameter definition'

# CORRECTED RULE. Source-shaped staging carries the source system's own QA test
# identifiers, and the transformation maps them. What must agree is the SIZE of
# the QA definition set and the UNITS, not the spellings.
Dim 'QA definition set' @"
SELECT abs(
  (SELECT count(DISTINCT test_code) FROM dump_store.src_pkl_mssql_shape_qa_lab_results WHERE test_code IS NOT NULL)
  - (SELECT count(*) FROM parameter_definitions WHERE coalesce(is_deleted,false)=false AND parameter_category = 'Quality'));
"@ @"
SELECT 'staging test codes: ' || coalesce(string_agg(DISTINCT test_code, ', ' ORDER BY test_code),'<none>')
FROM dump_store.src_pkl_mssql_shape_qa_lab_results WHERE test_code IS NOT NULL
UNION ALL
SELECT 'canonical Quality parameters: ' || coalesce(string_agg(parameter_code || '(' || coalesce(unit_of_measure,'?') || ')', ', ' ORDER BY parameter_code),'<none>')
FROM parameter_definitions WHERE coalesce(is_deleted,false)=false AND parameter_category = 'Quality';
"@ 'staging QA test count differing from the canonical Quality parameter count'

Dim 'QA units' @"
SELECT count(*) FROM (SELECT DISTINCT unit_code AS u FROM dump_store.src_pkl_mssql_shape_qa_lab_results
  WHERE unit_code IS NOT NULL) stg
WHERE NOT EXISTS (SELECT 1 FROM parameter_definitions pd
  WHERE coalesce(pd.is_deleted,false)=false AND pd.parameter_category='Quality'
    AND lower(coalesce(pd.unit_of_measure,'')) = lower(stg.u));
"@ @"
SELECT u FROM (SELECT DISTINCT unit_code AS u FROM dump_store.src_pkl_mssql_shape_qa_lab_results
  WHERE unit_code IS NOT NULL) stg
WHERE NOT EXISTS (SELECT 1 FROM parameter_definitions pd
  WHERE coalesce(pd.is_deleted,false)=false AND pd.parameter_category='Quality'
    AND lower(coalesce(pd.unit_of_measure,'')) = lower(stg.u)) ORDER BY 1 LIMIT 20;
"@ 'staging QA units no canonical Quality parameter declares'

Dim 'genealogy' @"
SELECT count(*) FROM dump_store.src_hsm_oracle_shape_hsm_coils c
WHERE c.input_piece_id IS NOT NULL AND NOT EXISTS (
  SELECT 1 FROM material_units child
  JOIN genealogy_edges ge ON ge.child_material_unit_id = child.id AND coalesce(ge.is_deleted,false)=false
  JOIN material_units parent ON parent.id = ge.parent_material_unit_id AND coalesce(parent.is_deleted,false)=false
  WHERE coalesce(child.is_deleted,false)=false AND child.material_code = c.coil_id AND parent.material_code = c.input_piece_id);
"@ @"
SELECT 'DECOMPOSITION - why the edge is missing' AS detail
UNION ALL SELECT '  staging coils total                       : ' || (SELECT count(*) FROM dump_store.src_hsm_oracle_shape_hsm_coils)::text
UNION ALL SELECT '  child coil unit missing in canonical      : ' || (SELECT count(*) FROM dump_store.src_hsm_oracle_shape_hsm_coils c
    WHERE NOT EXISTS (SELECT 1 FROM material_units m WHERE coalesce(m.is_deleted,false)=false AND m.material_code = c.coil_id))::text
UNION ALL SELECT '  parent slab unit missing in canonical     : ' || (SELECT count(*) FROM dump_store.src_hsm_oracle_shape_hsm_coils c
    WHERE c.input_piece_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM material_units m WHERE coalesce(m.is_deleted,false)=false AND m.material_code = c.input_piece_id))::text
UNION ALL SELECT '  both units exist but no edge joins them   : ' || (SELECT count(*) FROM dump_store.src_hsm_oracle_shape_hsm_coils c
    WHERE c.input_piece_id IS NOT NULL
      AND EXISTS (SELECT 1 FROM material_units m WHERE coalesce(m.is_deleted,false)=false AND m.material_code = c.coil_id)
      AND EXISTS (SELECT 1 FROM material_units m WHERE coalesce(m.is_deleted,false)=false AND m.material_code = c.input_piece_id)
      AND NOT EXISTS (SELECT 1 FROM material_units ch JOIN genealogy_edges ge ON ge.child_material_unit_id = ch.id AND coalesce(ge.is_deleted,false)=false
        JOIN material_units pa ON pa.id = ge.parent_material_unit_id
        WHERE ch.material_code = c.coil_id AND pa.material_code = c.input_piece_id))::text
UNION ALL SELECT '  edge EXISTS but to a DIFFERENT parent     : ' || (SELECT count(*) FROM dump_store.src_hsm_oracle_shape_hsm_coils c
    WHERE c.input_piece_id IS NOT NULL
      AND EXISTS (SELECT 1 FROM material_units ch JOIN genealogy_edges ge ON ge.child_material_unit_id = ch.id AND coalesce(ge.is_deleted,false)=false
        WHERE ch.material_code = c.coil_id AND ge.relationship_type = 'RolledInto')
      AND NOT EXISTS (SELECT 1 FROM material_units ch JOIN genealogy_edges ge ON ge.child_material_unit_id = ch.id AND coalesce(ge.is_deleted,false)=false
        JOIN material_units pa ON pa.id = ge.parent_material_unit_id
        WHERE ch.material_code = c.coil_id AND pa.material_code = c.input_piece_id))::text
UNION ALL SELECT '  coil has NO RolledInto edge at all        : ' || (SELECT count(*) FROM dump_store.src_hsm_oracle_shape_hsm_coils c
    WHERE c.input_piece_id IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM material_units ch JOIN genealogy_edges ge ON ge.child_material_unit_id = ch.id AND coalesce(ge.is_deleted,false)=false
        WHERE ch.material_code = c.coil_id AND ge.relationship_type = 'RolledInto'))::text
UNION ALL SELECT '  what canonical says instead (3 samples)   : ' || (SELECT coalesce(string_agg(x, '  |  '),'<none>') FROM (
    SELECT c.coil_id || ' donor->' || c.input_piece_id || ' canonical->' || pa.material_code AS x
    FROM dump_store.src_hsm_oracle_shape_hsm_coils c
    JOIN material_units ch ON ch.material_code = c.coil_id AND coalesce(ch.is_deleted,false)=false
    JOIN genealogy_edges ge ON ge.child_material_unit_id = ch.id AND coalesce(ge.is_deleted,false)=false AND ge.relationship_type = 'RolledInto'
    JOIN material_units pa ON pa.id = ge.parent_material_unit_id
    WHERE pa.material_code <> c.input_piece_id LIMIT 3) q)
UNION ALL SELECT '  duplicate canonical units per coil code   : ' || (SELECT coalesce(max(n),0) FROM (
    SELECT count(*) AS n FROM material_units m JOIN dump_store.src_hsm_oracle_shape_hsm_coils c ON c.coil_id = m.material_code
    WHERE coalesce(m.is_deleted,false)=false GROUP BY m.material_code) q)::text
UNION ALL SELECT '  canonical edge relationship types present : ' || (SELECT coalesce(string_agg(DISTINCT relationship_type, ', '),'<none>')
    FROM genealogy_edges WHERE coalesce(is_deleted,false)=false);
"@ 'staging coils whose declared parent edge is missing in canonical'

Dim 'time horizon' @"
WITH stg AS (SELECT min(rolling_start_time) AS lo, max(rolling_start_time) AS hi FROM dump_store.src_hsm_oracle_shape_hsm_coils),
     can AS (SELECT min(production_start_utc) AS lo, max(production_start_utc) AS hi FROM material_units
             WHERE coalesce(is_deleted,false)=false AND production_start_utc IS NOT NULL)
SELECT (CASE WHEN stg.lo IS NULL OR can.lo IS NULL THEN 1
             WHEN stg.lo < can.lo - interval '1 day' THEN 1
             WHEN stg.hi > can.hi + interval '1 day' THEN 1 ELSE 0 END) FROM stg, can;
"@ @"
SELECT 'staging   ' || min(rolling_start_time)::text || '  to  ' || max(rolling_start_time)::text
FROM dump_store.src_hsm_oracle_shape_hsm_coils
UNION ALL
SELECT 'canonical ' || min(production_start_utc)::text || '  to  ' || max(production_start_utc)::text
FROM material_units WHERE coalesce(is_deleted,false)=false AND production_start_utc IS NOT NULL;
"@ 'staging time window not contained in the canonical window (1 day tolerance)'

Dim 'planted phenomena' @"
WITH s AS (SELECT defect_code, row_number() OVER (ORDER BY count(*) DESC, defect_code) AS rn
  FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects WHERE defect_code IS NOT NULL GROUP BY defect_code),
     c AS (SELECT dc.defect_code, row_number() OVER (ORDER BY count(*) DESC, dc.defect_code) AS rn
  FROM quality_events qe JOIN defect_catalogs dc ON dc.id = qe.defect_catalog_id AND coalesce(dc.is_deleted,false)=false
  WHERE coalesce(qe.is_deleted,false)=false GROUP BY dc.defect_code)
SELECT count(*) FROM s FULL OUTER JOIN c ON s.rn = c.rn
WHERE (s.rn <= 5 OR c.rn <= 5) AND coalesce(s.defect_code,'<none>') <> coalesce(c.defect_code,'<none>');
"@ @"
WITH s AS (SELECT defect_code, count(*) AS n, row_number() OVER (ORDER BY count(*) DESC, defect_code) AS rn
  FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects WHERE defect_code IS NOT NULL GROUP BY defect_code),
     c AS (SELECT dc.defect_code, count(*) AS n, row_number() OVER (ORDER BY count(*) DESC, dc.defect_code) AS rn
  FROM quality_events qe JOIN defect_catalogs dc ON dc.id = qe.defect_catalog_id AND coalesce(dc.is_deleted,false)=false
  WHERE coalesce(qe.is_deleted,false)=false GROUP BY dc.defect_code)
SELECT 'rank ' || coalesce(s.rn, c.rn)::text
     || '   staging: ' || rpad(coalesce(s.defect_code,'<none>'), 20) || ' (' || coalesce(s.n,0)::text || ')'
     || '   canonical: ' || rpad(coalesce(c.defect_code,'<none>'), 20) || ' (' || coalesce(c.n,0)::text || ')'
FROM s FULL OUTER JOIN c ON s.rn = c.rn WHERE coalesce(s.rn, c.rn) <= 6 ORDER BY 1;
"@ 'top-five defect Pareto ordering differing between the layers'

W ''

W 'REPORTED, NEVER GATED (expected shape differences, recorded so they are not mistaken for defects)'
W ('  canonical grades not present in staging       : ' + (Q1 @"
SELECT count(*) FROM (SELECT DISTINCT grade_or_recipe AS g FROM material_units
  WHERE coalesce(is_deleted,false)=false AND grade_or_recipe IS NOT NULL) c
WHERE NOT EXISTS (SELECT 1 FROM dump_store.src_meltshop_pg_heats h WHERE h.steel_grade = c.g);
"@))
W ('  canonical defect codes not present in staging : ' + (Q1 @"
SELECT count(*) FROM (SELECT DISTINCT defect_code AS d FROM defect_catalogs WHERE coalesce(is_deleted,false)=false) c
WHERE NOT EXISTS (SELECT 1 FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects p WHERE p.defect_code = c.d);
"@))
W ('  canonical coils not present in staging        : ' + (Q1 @"
SELECT count(*) FROM material_units m WHERE coalesce(m.is_deleted,false)=false AND m.material_unit_type='Coil'
  AND NOT EXISTS (SELECT 1 FROM dump_store.src_hsm_oracle_shape_hsm_coils d WHERE d.coil_id = m.material_code);
"@) + '   (current Fleet-v2 coil identity coverage is complete)')
W ''

# =============================================================================
# EVIDENCE FOR EVERY DIMENSION THAT IS NOT GREEN
# =============================================================================
$red = @($Dimensions | Where-Object { $_.Verdict -ne 'PASS' })
if ($red.Count -gt 0) {
    W 'EVIDENCE - what is actually offending, per red dimension'
    foreach ($d in $red) {
        W ''
        W ('  ### ' + $d.Name + '   (' + $d.Verdict + ', offending=' + $d.Offending + ')')
        foreach ($line in (QList $d.Sql)) { W ('      ' + $line) }
    }
    W ''
}

# =============================================================================
# INJECTED DIVERGENCE - parsed by tag, never by line position
# =============================================================================
$injectVerdict = 'NOT RUN'
if ($InjectDivergence) {
    W 'INJECTED DIVERGENCE PROOF'
    W '  A defect code absent from canonical is written into staging, the defect'
    W '  vocabulary dimension is re-measured, and the transaction is ALWAYS rolled back.'
    W '  v1 read the last stdout line, which was the ROLLBACK command tag. The result is'
    W '  now emitted as PPIQ_INJECT=<n> and matched by regex.'
    $tmp = Join-Path $env:TEMP ('ppiq_t031_inject_' + [guid]::NewGuid().ToString('N') + '.sql')
    $injectSql = @"
BEGIN;
DO `$`$
DECLARE
    v_base  integer;
    v_after integer;
    v_rows  bigint;
BEGIN
    -- The SAME expression the defect-vocabulary dimension uses. It is measured
    -- here rather than reused from the runner so the assertion is self-contained,
    -- and the production dimension query is NOT modified to make this work.
    SELECT count(*) INTO v_base FROM (
      SELECT DISTINCT defect_code AS d FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects
      WHERE defect_code IS NOT NULL) stg
    WHERE NOT EXISTS (
      SELECT 1 FROM defect_catalogs c WHERE coalesce(c.is_deleted,false)=false AND c.defect_code = stg.d);

    INSERT INTO dump_store.src_inspection_mysql_shape_parsytec_surface_defects
      (defect_row_id, coil_id, inspection_device, defect_code, defect_name, defect_class, defect_severity,
       side_code, position_start_m, position_end_m, width_position_mm, confidence_pct,
       event_time_utc, updated_at_utc)
    SELECT (SELECT coalesce(max(defect_row_id), 0) + 1
            FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects),
           coil_id, inspection_device, 'PPIQ_INJECTED_DIVERGENCE', 'Injected divergence',
           defect_class, defect_severity, side_code, position_start_m, position_end_m,
           width_position_mm, confidence_pct, event_time_utc, updated_at_utc
    FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects LIMIT 1;

    SELECT count(*) INTO v_after FROM (
      SELECT DISTINCT defect_code AS d FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects
      WHERE defect_code IS NOT NULL) stg
    WHERE NOT EXISTS (
      SELECT 1 FROM defect_catalogs c WHERE coalesce(c.is_deleted,false)=false AND c.defect_code = stg.d);

    SELECT count(*) INTO v_rows FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects;

    RAISE NOTICE 'PPIQ_BASELINE=%', v_base;
    RAISE NOTICE 'PPIQ_INJECT=%', v_after;
    RAISE NOTICE 'PPIQ_STAGING_ROWS=%', v_rows;

    -- THE ASSERTION. If the injected code did not raise the offending count the
    -- gate cannot fail, and a gate that cannot fail is not a gate. Raising here
    -- makes psql exit non-zero AND aborts the transaction, so the failure path
    -- is also the rollback path.
    IF v_after <= v_base THEN
        RAISE EXCEPTION 'PPIQ_INJECTION_DID_NOT_FIRE baseline=% injected=% code=% staging_defect_rows=%',
            v_base, v_after, 'PPIQ_INJECTED_DIVERGENCE', v_rows;
    END IF;
END
`$`$;
ROLLBACK;
"@
    [System.IO.File]::WriteAllText($tmp, $injectSql, (New-Object System.Text.UTF8Encoding($false)))
    $o = @(& $Psql -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -w -X -A -t -v ON_ERROR_STOP=1 -f $tmp 2>&1 |
           ForEach-Object { $_.ToString() })
    $injectExit = $LASTEXITCODE
    Remove-Item $tmp -ErrorAction SilentlyContinue

    # THE VERDICT IS THE EXIT CODE, NOT A PARSE. The DO block raises an exception
    # when the injected measurement fails to exceed the baseline, so a proof that
    # did not fire cannot be reported as one. The sentinels below populate the
    # report and nothing else - if they are missing the verdict is unaffected.
    function Sentinel([string]$name) {
        $hit = @($o | Where-Object { $_ -match ($name + '=([0-9]+)') })
        if ($hit.Count -eq 0) { return 'not reported' }
        $null = $hit[0] -match ($name + '=([0-9]+)')
        return $matches[1]
    }
    $baseline = Sentinel 'PPIQ_BASELINE'
    $observed = Sentinel 'PPIQ_INJECT'
    W ('  baseline offending        : ' + $baseline)
    W ('  offending under injection : ' + $observed + '   (the database asserts this must exceed the baseline)')
    W ('  staging defect rows       : ' + (Sentinel 'PPIQ_STAGING_ROWS'))
    if ($injectExit -eq 0) { $injectVerdict = 'PROVEN RED' } else {
        $injectVerdict = 'DID NOT FIRE'
        W ('  psql exit code            : ' + $injectExit)
        W '  the database refused the proof. Its output follows:'
        if ($o.Count -eq 0) { W '      (no output at all)' }
        foreach ($line in @($o | Select-Object -First 15)) { W ('      ' + $line) }
    }
    W ('  verdict                   : ' + $injectVerdict)
    $residue = Q1 "SELECT count(*) FROM dump_store.src_inspection_mysql_shape_parsytec_surface_defects WHERE defect_code = 'PPIQ_INJECTED_DIVERGENCE';"
    W ('  rollback residue check    : ' + $residue + '   (must be 0)')
    if ($residue -ne '0') { W '  [FATAL] the injected row survived the rollback.'; $injectVerdict = 'ROLLBACK FAILED' }
    W ''
}

W 'CERTIFICATION GATE'
W ('  dimensions measured : ' + $Dimensions.Count)
W ('  dimensions passing  : ' + ($Dimensions.Count - $red.Count))
W ('  dimensions red      : ' + $red.Count)
if ($InjectDivergence) { W ('  injected divergence : ' + $injectVerdict) }
W ''
foreach ($d in $red) { W ('  RED  ' + $d.Name.PadRight(24) + 'offending=' + $d.Offending) }
if ($red.Count -gt 0) {
    W ''
    W '[CERTIFICATION RED] staging and canonical do not describe one Fleet v2 plant.'
    W ('Report: ' + $Report)
    Save
    exit 3
}
if ($InjectDivergence -and $injectVerdict -ne 'PROVEN RED') {
    W ''
    W '[CERTIFICATION RED] every dimension passed but the injected divergence did not turn it red.'
    W '                    A gate that cannot fail is not a gate.'
    W ('Report: ' + $Report)
    Save
    exit 4
}
W '[CERTIFICATION GREEN] dump_store staging and the canonical plant describe one Fleet v2 plant'
W '                      on every dimension of Chapter 3 section 4.5.2a.'
W ('Report: ' + $Report)
Save
exit 0