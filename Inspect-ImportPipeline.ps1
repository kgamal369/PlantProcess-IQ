# ============================================================================
# Inspect-ImportPipeline.ps1
#
# THE QUESTION THIS SETTLES: has this pipeline EVER moved a row?
# The batch dump shows "status":"Completed","row_count":0 on a Meltshop batch.
# If that holds across all 16, then every import ever run here completed
# successfully and imported nothing - and the 40,148 material_units came from
# the phase-3 dump restore, not from the pipeline. That changes what M1-20 is:
# not "run the runsheet" but "fix the connector, THEN run the runsheet".
#
# Known suspects (found last week, none retested since):
#   - null cursor builds an invalid WHERE clause -> zero rows, no error
#   - cursor sent as text against timestamptz (Postgres 42883)
#   - DateTime locale sensitivity on a German machine
#   - POST /admin/connectors/datasets returns 500 after successful persist
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Inspect-ImportPipeline.ps1
# ============================================================================
[CmdletBinding()]
param([string]$TargetDb = 'ppiq_presentation')

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$Out = Join-Path $RepoRoot ('ImportPipeline_' + $Stamp + '.txt')
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }
function Save { [System.IO.File]::WriteAllText($Out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false))) }

$Psql = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $Psql = $cmd.Source } else {
    $c = @(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if ($c.Count -gt 0) { $Psql = $c[0].FullName }
}
if (-not $Psql) { Write-Host "[FAIL] psql not found." -ForegroundColor Red; exit 1 }
$env:PGPASSWORD = 'ppiq_dev_local_only'
function Rows([string]$q) {
    return @(& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F ' | ' -c $q 2>&1 | Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}
$probe = @(Rows "SELECT 40148;")
if (@($probe).Count -eq 0 -or "$($probe[0])".Trim() -ne '40148') { Write-Host "[SELF-CHECK FAILED]" -ForegroundColor Red; exit 1 }

W ("IMPORT PIPELINE INSPECTION - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + "   DB: " + $TargetDb)
W ("=" * 78)
W ""

# ---- 1. every batch, with the number that matters --------------------------
W "[1] EVERY import batch - the row_count column decides everything:"
W ("    {0,-52} {1,-12} {2,>8}  {3}" -f 'batch_code', 'status', 'rows', 'error')
Rows @"
SELECT rpad(left(import_batch_code,50),52) || ' ' || rpad(COALESCE(status,'?'),12) || ' ' ||
       lpad(COALESCE(row_count::text,'-'),8) || '  ' || COALESCE(left(error_message,60),'')
FROM import_batches ORDER BY started_at_utc;
"@ | ForEach-Object { W ("    " + $_) }
W ""
$tot = @(Rows "SELECT COALESCE(SUM(row_count),0) FROM import_batches;")
$nz = @(Rows "SELECT COUNT(*) FROM import_batches WHERE COALESCE(row_count,0) > 0;")
$totV = '0'; if (@($tot).Count) { $totV = "$($tot[0])".Trim() }
$nzV = '0'; if (@($nz).Count) { $nzV = "$($nz[0])".Trim() }
W ("    TOTAL ROWS EVER IMPORTED : " + $totV)
W ("    BATCHES WITH row_count>0 : " + $nzV + " of 16")
W ""
if ($totV -eq '0') {
    W "    *****************************************************************"
    W "    VERDICT: THE PIPELINE HAS NEVER IMPORTED A ROW."
    W "    Every batch reports Completed and moved nothing. The 40,148"
    W "    material_units in this database came from the phase-3 dump"
    W "    restore, NOT from the import pipeline."
    W ""
    W "    This is THE finding of the week. It means:"
    W "      - journey step 3 (Import) has never been proven end-to-end here"
    W "      - M1-20 is not 'run the runsheet', it is 'fix the connector first'"
    W "      - a silent success is worse than a failure: the product tells the"
    W "        operator Completed while importing nothing"
    W "    *****************************************************************"
} else {
    W ("    The pipeline HAS moved rows (" + $totV + " total). Zero-row batches are delta runs with nothing new - normal.")
}
W ""

# ---- 2. the cursors - prime suspect ----------------------------------------
W "[2] dataset cursors (null cursor -> invalid WHERE -> zero rows, no error):"
W ("    {0,-34} {1,-22} {2,-24} {3}" -f 'dataset_code', 'cursor_field', 'last_cursor_value', 'active')
Rows @"
SELECT rpad(left(dataset_code,32),34) || ' ' || rpad(COALESCE(incremental_cursor_field,'(NULL)'),22) || ' ' ||
       rpad(COALESCE(left(last_cursor_value,22),'(NULL)'),24) || ' ' || is_active::text
FROM source_dataset_definitions ORDER BY dataset_code;
"@ | ForEach-Object { W ("    " + $_) }
W ""
$nullCur = @(Rows "SELECT COUNT(*) FROM source_dataset_definitions WHERE last_cursor_value IS NULL;")
$ncV = '?'; if (@($nullCur).Count) { $ncV = "$($nullCur[0])".Trim() }
W ("    datasets with NULL last_cursor_value: " + $ncV)
W "    (a NULL cursor on first run is normal ONLY if the connector treats it as"
W "     'no lower bound'. If it interpolates NULL into the WHERE clause, the"
W "     predicate is never true and the import returns zero rows - silently.)"
W ""

# ---- 3. staging: did anything land, ever? ----------------------------------
W "[3] staging_records by batch (the pipeline's first landing zone):"
$stgBatch = @(Rows "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='staging_records' AND column_name ~* 'batch' LIMIT 1;")
if (@($stgBatch).Count -eq 0) {
    W "    no batch column on staging_records"
} else {
    $bc = "$($stgBatch[0])".Trim()
    W ("    (join column: staging_records." + $bc + ")")
    Rows ("SELECT COALESCE(" + $bc + "::text,'(null)') || '  ' || COUNT(*) FROM staging_records GROUP BY 1 ORDER BY 2 DESC LIMIT 10;") |
        ForEach-Object { W ("    " + $_) }
    W ""
    $orphan = @(Rows ("SELECT COUNT(*) FROM staging_records s WHERE NOT EXISTS (SELECT 1 FROM import_batches b WHERE b.id::text = s." + $bc + "::text);"))
    $oV = '?'; if (@($orphan).Count) { $oV = "$($orphan[0])".Trim() }
    W ("    staging rows NOT linked to any import batch: " + $oV)
    W "    (if that number is ~16,640, the staging rows came from the dump too,"
    W "     and the batch->staging link cannot be demonstrated on this data)"
}
W ""

# ---- 4. what is registered vs what Phase A needs ---------------------------
W "[4] registered datasets vs the four Phase-A taxonomy views:"
$have = @(Rows "SELECT lower(source_object_name) FROM source_dataset_definitions;")
foreach ($need in @('v_parameter_definitions (Meltshop CP-01)', 'V_PARAMETER_DEFINITIONS (Caster CP-06)', 'V_PARAMETER_DEFINITIONS (HSM CP-04)', 'v_defect_definitions (Parsytec CP-03)')) {
    $obj = ($need -split ' ')[0].ToLower()
    $hit = $false
    foreach ($h in $have) { if ("$h".Trim() -eq $obj) { $hit = $true; break } }
    W ("    " + $(if ($hit) { '[REGISTERED]' } else { '[MISSING]   ' }) + " " + $need)
}
W ""
W "    currently registered:"
Rows "SELECT '      ' || dataset_code || '  ->  ' || COALESCE(source_schema_name,'?') || '.' || source_object_name FROM source_dataset_definitions ORDER BY 1;" |
    ForEach-Object { W $_ }
W ""
W "=" * 78
W "NEXT MOVE DEPENDS ON SECTION 1:"
W "  TOTAL = 0  -> the connector is the M1 blocker. Do not run the runsheet;"
W "              fix the zero-row path first, then Phase A proves itself."
W "  TOTAL > 0  -> the pipeline works; Phase A is simply unstarted. Register"
W "              the four taxonomy views in Prepare Import and run them."
Save
Write-Host ""
Write-Host ("[DONE] -> " + $Out) -ForegroundColor Green
exit 0
