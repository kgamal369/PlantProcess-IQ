<#
    PPIQ runner: Show-PpiqT073EvidenceState

    READ-ONLY. Writes nothing, changes nothing, and takes no -Apply.

    It exists because the certification produced three numbers that cannot all be
    true under the design as written: 38 widget-result chunks produced, 4 rows in
    canon.assistant_widget_result, 3 widget-result rows in canon.assistant_chunk.
    Every produced chunk carries a distinct evidence id as its source_ref, and
    every evidence id comes from a distinct result_fingerprint that hashes the
    page code and the widget code. So 38 cannot fold onto 4 unless something I
    believe about that path is wrong.

    This prints what is actually there. No fix is written until it has run.
#>

[CmdletBinding()]
param([string]$PgDb = "")

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

function Show([string]$Title, [string]$Query) {
    Head $Title
    & psql -h 127.0.0.1 -p 5432 -U $PgUser -d $PgDb -w -X -v ON_ERROR_STOP=1 -c $Query
}

W ("Database: " + $PgDb + " as " + $PgUser)

Show "1. HOW MANY WIDGETS DOES DISCOVERY ACTUALLY SEE" @"
SELECT count(*) AS active_widgets,
       count(DISTINCT d.dashboard_code) AS pages,
       count(DISTINCT w.widget_code)    AS distinct_widget_codes
FROM public.dashboard_widget_definitions w
JOIN public.dashboard_definitions d ON d.id = w.dashboard_definition_id
WHERE w.is_deleted = false AND w.is_active = true
  AND d.is_deleted = false AND d.is_active = true;
"@

Show "2. THE EVIDENCE TABLE, IN FULL" @"
SELECT count(*)                              AS rows,
       count(DISTINCT page_code)             AS pages,
       count(DISTINCT widget_code)           AS widgets,
       count(DISTINCT query_fingerprint)     AS query_fingerprints,
       count(DISTINCT result_fingerprint)    AS result_fingerprints
FROM canon.assistant_widget_result;
"@

Show "3. EVERY EVIDENCE ROW, NEWEST FIRST" @"
SELECT left(id::text, 8)                AS id,
       page_code,
       widget_code,
       left(query_fingerprint, 10)      AS query_fp,
       left(result_fingerprint, 10)     AS result_fp,
       population_count,
       generated_at_utc,
       created_at_utc
FROM canon.assistant_widget_result
ORDER BY created_at_utc DESC
LIMIT 40;
"@

Show "4. THE CHUNK TABLE - WHAT SURVIVED THE UPSERT" @"
SELECT source_kind,
       count(*)                        AS chunk_rows,
       count(DISTINCT source_ref)      AS distinct_refs,
       count(*) FILTER (WHERE is_stale) AS stale
FROM canon.assistant_chunk
GROUP BY source_kind
ORDER BY source_kind;
"@

Show "5. WIDGET-RESULT CHUNKS, AND WHETHER THEIR REF IS A REAL EVIDENCE ROW" @"
SELECT left(c.source_ref, 8) AS source_ref,
       (e.id IS NOT NULL)    AS resolves,
       e.page_code,
       e.widget_code,
       left(c.content, 90)   AS sentence_start
FROM canon.assistant_chunk c
LEFT JOIN canon.assistant_widget_result e
       ON e.id::text = c.source_ref AND e.tenant_id = c.tenant_id
WHERE c.source_kind = 'widgetresult'
ORDER BY c.source_ref;
"@

Show "6. DO THE EVIDENCE ROWS SHARE A TENANT WITH THE CHUNKS" @"
SELECT 'evidence' AS table_name, tenant_id, count(*) FROM canon.assistant_widget_result GROUP BY tenant_id
UNION ALL
SELECT 'chunks',                 tenant_id, count(*) FROM canon.assistant_chunk WHERE source_kind = 'widgetresult' GROUP BY tenant_id;
"@

Show "7. WHICH ROW IS NEW ON EACH RUN" @"
SELECT date_trunc('second', created_at_utc) AS created,
       count(*)                             AS rows_created,
       string_agg(DISTINCT widget_code, ', ') AS widgets
FROM canon.assistant_widget_result
GROUP BY 1
ORDER BY 1 DESC;
"@

Head "WHAT TO READ"
W "If (1) reports ~38 active widgets and (2) reports 4 rows, the persist path is"
W "folding distinct widgets onto one identity, and section 3 shows which."
W ""
W "If (1) reports 4 active widgets, then 38 chunks were produced from 4 widgets,"
W "which would mean the producer runs the widget loop more than once per reindex."
W ""
W "Section 5 is the one that matters most for the demonstration: any row where"
W "resolves = false is a citation the assistant can show and the evidence"
W "endpoint cannot open."
exit 0
