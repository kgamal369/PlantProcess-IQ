# ============================================================================
# Measure-PpiqT010GapsV3.ps1        Backlog v2.4 task T-010, discovery pass
#
# READ-ONLY. Creates nothing, alters nothing, writes one evidence file.
#
# WHAT v2 GOT WRONG, ALL THREE MINE
#   1. SELECT format(...) returns ONE column. I then wrote ORDER BY 1,2,3 and
#      ORDER BY 2 DESC against it. Six queries failed on "ORDER BY position 2
#      is not in select list". v3 drops format() entirely - psql -A -F "|"
#      already separates columns, so ordering is correct by construction and
#      the query is short enough to be obviously right.
#   2. mapping_definitions.target_entity does not exist. I GUESSED A COLUMN
#      NAME AGAIN. v3 never names a column it has not first read from
#      information_schema; where the shape is unknown it PRINTS THE SHAPE.
#   3. Section 4b said "unreadable" for every row because a single-row result
#      unrolls to a scalar and, under StrictMode, a scalar has no .Count. Same
#      defect as the Detail-mode PickCol this morning. Every call site now
#      wraps in @().
#
# RUN FROM REPO ROOT. Commands at the bottom.
# ============================================================================
[CmdletBinding()]
param(
    [string]$TargetDb = "ppiq_presentation",
    [string]$DbHost   = "127.0.0.1",
    [int]   $Port     = 5432,
    [string]$User     = "ppiq_dev",
    [string]$Password = "ppiq_dev_local_only"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

$RepoRoot    = (Get-Location).Path
$EvidenceDir = Join-Path $RepoRoot "docs\m1\evidence"
$Stamp       = Get-Date -Format "yyyyMMdd_HHmmss"
$EvidencePath = Join-Path $EvidenceDir ("T-010_gap_measurement_" + $Stamp + ".txt")

$env:PGPASSWORD = $Password
$env:PGCLIENTENCODING = "UTF8"

$Lines = New-Object System.Collections.ArrayList
function Say([string]$Line) { Write-Host $Line; [void]$Lines.Add($Line) }
function Head([string]$Banner) { Say ""; Say ("=" * 78); Say $Banner; Say ("=" * 78) }

# SQL to a FILE, run with -f, results via -o, stderr captured. Never -c.
function Rows([string]$Sql) {
    $gid = [guid]::NewGuid().ToString("N")
    $qF = Join-Path $env:TEMP ("ppiq_t010q_" + $gid + ".sql")
    $rF = Join-Path $env:TEMP ("ppiq_t010r_" + $gid + ".txt")
    $eF = Join-Path $env:TEMP ("ppiq_t010e_" + $gid + ".txt")
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($qF, $Sql, $enc)

    & psql -h $DbHost -p $Port -U $User -d $TargetDb -w -X -A -F "|" -t -v ON_ERROR_STOP=1 -o $rF -f $qF 2>$eF | Out-Null
    $rc = $LASTEXITCODE

    $res = @()
    if (Test-Path $rF) { $res = @([System.IO.File]::ReadAllText($rF) -split "`r?`n" | Where-Object { $_ -ne "" }) }
    $errText = ""
    if (Test-Path $eF) { $errText = ([System.IO.File]::ReadAllText($eF)).Trim() }
    foreach ($f in @($qF, $rF, $eF)) { Remove-Item $f -ErrorAction SilentlyContinue }

    if ($rc -ne 0) {
        $msg = @("QUERY FAILED, exit " + $rc)
        foreach ($el in ($errText -split "`r?`n")) {
            $tl = $el.Trim()
            if ($tl -ne "" -and $tl -notmatch '^(At |\+ |CategoryInfo|FullyQualifiedErrorId)') { $msg += ("   " + $tl) }
        }
        return $msg
    }
    return $res
}
function Show([string]$Label, [string]$Sql) {
    Say ""
    Say $Label
    $r = @(Rows $Sql)
    if ($r.Count -eq 0) { Say "   (no rows)"; return }
    foreach ($x in $r) { Say ("   " + $x) }
}
function One([string]$Sql) {
    $r = @(Rows $Sql)
    if ($r.Count -eq 0) { return "unreadable" }
    return ([string]$r[0]).Trim()
}

Head ("T-010 GAP MEASUREMENT v3 - " + $TargetDb + " - " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
Say "Read-only. Nothing is created or altered."

# ------------------------------------------------------- 1. DOWNTIME --------
Head "1. DOWNTIME - NOT A LOSSY PROJECTION. NO PROJECTION AT ALL."
Say "v2 proved it: the three canonical rows are DT-CAST-SPEED-HOLD, DT-HSM-SENSOR"
Say "and DT-NO-REFERENCE, all source_system ADVANCED_DEMO_SEED, and ZERO of the"
Say "210 staged ids appear in canonical. They are hand-seeded fixture rows. The"
Say "210 staged rows have never been projected. A missing mapping and a lossy"
Say "mapping need different fixes."

Show "Canonical downtime rows by source_system:" @"
SELECT COALESCE(source_system, '(null)'), count(*)
FROM downtime_events
GROUP BY 1
ORDER BY 2 DESC;
"@

Show "Tables whose name suggests a mapping definition, and their row counts:" @"
SELECT n.nspname, c.relname,
       (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', n.nspname, c.relname), false, true, '')))[1]::text
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r' AND c.relname ~* 'mapping'
ORDER BY 1, 2;
"@

# The shape is UNKNOWN, so print the shape instead of guessing a column name.
Show "Columns of every mapping-ish table, because v2 guessed 'target_entity' and it does not exist:" @"
SELECT table_schema, table_name, column_name, data_type
FROM information_schema.columns
WHERE table_name ~* 'mapping'
  AND table_schema NOT IN ('pg_catalog', 'information_schema')
ORDER BY table_schema, table_name, ordinal_position;
"@

Show "Staged downtime source, shape and a sample of the data:" @"
SELECT equipment_code, reason_code, downtime_category, duration_seconds
FROM src_inspection_mysql_shape.downtime_events
ORDER BY duration_seconds DESC
LIMIT 12;
"@

Show "Staged downtime spread - the material for charts 13 to 16:" @"
SELECT equipment_code, count(*), min(duration_seconds), max(duration_seconds), round(avg(duration_seconds))
FROM src_inspection_mysql_shape.downtime_events
GROUP BY 1
ORDER BY 2 DESC;
"@

Show "Distinct downtime reasons and categories staged:" @"
SELECT downtime_category, reason_code, count(*)
FROM src_inspection_mysql_shape.downtime_events
GROUP BY 1, 2
ORDER BY 3 DESC;
"@

# ------------------------------------------------- 2. SHIFT OR CREW ---------
Head "2. DOES A SHIFT OR CREW ATTRIBUTE ALREADY EXIST?"
Say "T-010 says add one ONLY IF the sources do not already carry one. A"
Say "measurement, not an assumption."

Show "Any column named for a shift, crew, rota or team, anywhere:" @"
SELECT table_schema, table_name, column_name
FROM information_schema.columns
WHERE column_name ~* '(shift|crew|rota|team)'
  AND table_schema NOT IN ('pg_catalog', 'information_schema')
ORDER BY table_schema, table_name, column_name;
"@

Show "Timestamp columns on the staged sources, to derive a shift from:" @"
SELECT table_schema, table_name, column_name
FROM information_schema.columns
WHERE table_schema LIKE 'src\_%' AND data_type LIKE 'timestamp%'
ORDER BY table_schema, table_name, column_name;
"@

# ------------------------------------------ 3. GRADE SPECIFICATION ----------
Head "3. DOES A GRADE SPECIFICATION EXIST?"
Say "Chart 12 needs per-grade chemistry minima and maxima, drawn from the"
Say "customer's own data and never from a literal in the product."

Show "Any specification table or min/max column, anywhere:" @"
SELECT table_schema, table_name, column_name
FROM information_schema.columns
WHERE (column_name ~* '(spec|minimum|maximum|tolerance|limit)' OR table_name ~* 'spec')
  AND table_schema NOT IN ('pg_catalog', 'information_schema')
ORDER BY table_schema, table_name, column_name;
"@

Show "Grade-bearing columns, so the specification is keyed to a real name:" @"
SELECT table_schema, table_name, column_name
FROM information_schema.columns
WHERE column_name ~* 'grade'
  AND table_schema NOT IN ('pg_catalog', 'information_schema')
ORDER BY table_schema, table_name, column_name;
"@

Show "Every column on the meltshop heats source - the chemistry lives here:" @"
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'src_meltshop_pg' AND table_name = 'heats'
ORDER BY ordinal_position;
"@

Show "Every column on lf_treatment, which was missing from my T-007 inventory:" @"
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'src_meltshop_pg' AND table_name = 'lf_treatment'
ORDER BY ordinal_position;
"@

# ------------------------------------------ 4. STAGED SOURCE INVENTORY ------
Head "4. STAGED SOURCE INVENTORY"
Show "Every staged table with its row count:" @"
SELECT n.nspname || '.' || c.relname,
       (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', n.nspname, c.relname), false, true, '')))[1]::text
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r' AND n.nspname LIKE 'src\_%'
ORDER BY 1;
"@

Head "4b. MEASURED SCALE AGAINST WHAT FLEET_RELATIONS.md CLAIMS"
Say "T-007 artifact 1 quoted these as MEASURED. They were DOCUMENTED. Mine to own."
Say ""
Say "   structure               documented      measured        ratio"
foreach ($pair in @(
    @("heats", 1802, "src_meltshop_pg.heats"),
    @("casting sequences", 956, "src_caster_oracle_shape.cast_sequence"),
    @("slabs", 18661, "src_caster_oracle_shape.cast_pieces"),
    @("coils", 18661, "src_hsm_oracle_shape.hsm_coils"),
    @("HSM stand passes", 111966, "src_hsm_oracle_shape.hsm_pass_measurements"),
    @("surface defects", 34312, "src_inspection_mysql_shape.parsytec_surface_defects"),
    @("pickled coils", 15782, "src_pkl_mssql_shape.pickle_orders"),
    @("QA tests", 8920, "src_pkl_mssql_shape.qa_lab_results"))) {
    $doc = [int]$pair[1]
    $mv = One ("SELECT count(*) FROM " + $pair[2] + ";")
    $ratio = "n/a"
    if ($mv -ne "unreadable" -and $doc -gt 0) {
        $ratio = ("{0:N2}" -f ([double]$mv / [double]$doc))
    }
    Say ("   " + ([string]$pair[0]).PadRight(24) + ([string]$doc).PadLeft(10) + ([string]$mv).PadLeft(14) + ([string]$ratio).PadLeft(13))
}
Say ""
Say "A UNIFORM ratio means the generator simply ran at a smaller scale, which is"
Say "recoverable. A ratio that collapses for one structure means something else,"
Say "and the defect charts are the ones to look at hardest."

Show "Defect density, the number that decides charts 8, 9, 11 and 25 to 30:" @"
SELECT (SELECT count(*) FROM src_inspection_mysql_shape.parsytec_surface_defects) AS defects,
       (SELECT count(*) FROM src_hsm_oracle_shape.hsm_coils) AS coils,
       round((SELECT count(*) FROM src_inspection_mysql_shape.parsytec_surface_defects)::numeric
             / NULLIF((SELECT count(*) FROM src_hsm_oracle_shape.hsm_coils), 0), 3) AS per_coil;
"@

Show "Distinct defect codes staged - the Pareto needs six to ten meaningful classes:" @"
SELECT defect_code, count(*)
FROM src_inspection_mysql_shape.parsytec_surface_defects
GROUP BY 1
ORDER BY 2 DESC;
"@

# ------------------------------------------------------------- VERDICT ------
Head "5. WHAT THIS DECIDES"
Say "GAP 1, SHIFT      : section 2 decides it. No column found means derive from"
Say "                    the production timestamp, which is T-010's own fallback."
Say "GAP 2, GRADE SPEC : section 3 names the grade key and the real chemistry"
Say "                    columns. No column name is invented from here on."
Say "GAP 3, DOWNTIME   : section 1 shows 210 staged rows with equipment, reason,"
Say "                    category and duration - everything a projection needs."
Say "                    The gap is that NO MAPPING EXISTS, not that data is thin."
Say ""
Say "RULING REQUIRED, outside T-010's written scope: T-010 says only 'the two"
Say "downtime quantities populated independently once the schema slice lands'."
Say "There is nothing to populate - the 210 staged rows were never projected."
Say "Does T-010 include authoring the downtime mapping, or is that a separate"
Say "item? MY READING: separate. It is a mapping gap, not an emulated-source gap."
Say "But charts 13 to 18 are blocked until it is done, whoever does it."

New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null
$body = ($Lines.ToArray() -join "`r`n")
$sb = New-Object System.Text.StringBuilder
foreach ($ch in $body.ToCharArray()) { if ([int]$ch -le 126 -and [int]$ch -ge 9) { [void]$sb.Append($ch) } }
$enc2 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($EvidencePath, $sb.ToString(), $enc2)
Write-Host ""
Write-Host ("[EVIDENCE] " + $EvidencePath)

# ============================================================================
# HOW TO RUN
#
#   cd C:\Workspace\PlantProcess-IQ
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Measure-PpiqT010GapsV3.ps1
#
#   git add -A
#   git commit -m "T-010: gap measurement before implementation"
# ============================================================================
