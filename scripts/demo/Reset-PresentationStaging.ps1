# =============================================================================
# Reset-PresentationStaging.ps1        v2
#
# THE OPERATIONAL PRESENTATION STAGING RESET.
#
# DOCTRINE (Chapter 3 section 4.5.2a rule 4, not re-litigated here):
#   src_*       source-shaped DONOR. NOT staging.
#   dump_store  the current transitional physical STAGING layer.
#   canonical   the current plant model.
#
# RESPONSIBILITY
#   back up staging and the registry, in TWO dumps
#   compare each registered dump table's shape against its donor table
#   drop and re-register any dump table whose shape is stale
#   clear the population and null the stage-1 watermarks
#   run the EXISTING ppiq_run_stage1_delta_import_all
#   verify staging against the donor and against canonical
#
# WHY THE SHAPE REFRESH LIVES HERE AND NOT IN THE DONOR RUNNER
#   Fleet-v2 mode widens the donor schema, and ppiq_register_dump_source uses
#   CREATE TABLE IF NOT EXISTS so it will not widen an existing dump table.
#   Stage 1 builds its column list from the SOURCE, so a stale dump table makes
#   it insert columns that do not exist. The refresh is DESTRUCTIVE, so it
#   belongs after the STAGING backup - which only this runner takes.
#
# WHY THE BACKUP IS TWO FILES
#   pg_dump lets -t win over -n. A single invocation carrying both wrote a file
#   holding only the registry table while reporting success, and v1 printed that
#   0.01 MB file as proof. Each part is now dumped and size-checked separately.
#
# WHAT THIS NEVER DOES
#   It never runs stage 2. ppiq_run_stage2_canonical_refresh writes
#   material_units, genealogy_edges and quality_events, so running it would
#   rewrite the canonical plant that T-024 emitted and T-025 computed against.
#   It never adds a product function or endpoint - stage 1 already full-loads
#   when last_index_value_text IS NULL.
#   It never touches the donor schemas.
#
# Run from repo root:
#   .\scripts\demo\Reset-PresentationStaging.ps1
#   .\scripts\demo\Reset-PresentationStaging.ps1 -Execute
# =============================================================================
[CmdletBinding()]
param(
    [switch]$Execute,
    [string]$TargetDb = 'ppiq_presentation',
    [string]$DbHost = '127.0.0.1',
    [int]$DbPort = 5432,
    [string]$DbUser = 'ppiq_dev',
    [string]$BackupDir = 'deploy\.ppiq-snapshots'
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

if ($TargetDb -notmatch 'presentation') {
    Write-Host "[REFUSED] guard: target database name must contain 'presentation'." -ForegroundColor Red
    exit 1
}

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$EvidenceDir = Join-Path $RepoRoot 'docs\m1\evidence'
if (-not (Test-Path $EvidenceDir)) { New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null }
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
$PgDump = Join-Path $PgBin 'pg_dump.exe'
$env:PGPASSWORD = 'ppiq_dev_local_only'

function Q1([string]$q) {
    $o = & $Psql -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -w -X -A -t -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { return $null }
    $l = @($o | Where-Object { $_ -and ($_.ToString().Trim() -ne '') }) | Select-Object -First 1
    if ($null -eq $l) { return '' }
    return $l.ToString().Trim()
}
function Rows([string]$q) {
    return @(& $Psql -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -w -X -A -t -F '|' -c $q 2>&1 |
             Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}
function RunFile([string]$label, [string]$path) {
    $o = & $Psql -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -w -X -v ON_ERROR_STOP=1 -f $path 2>&1
    $code = $LASTEXITCODE
    if ($code -eq 0) { W ('      OK   ' + $label) } else {
        W ('      FAIL ' + $label)
        @($o | Select-Object -Last 8) | ForEach-Object { W ('           ' + $_) }
    }
    return ($code -eq 0)
}
function RunSql([string]$label, [string]$sql) {
    $p = Join-Path $env:TEMP ('ppiq_' + [guid]::NewGuid().ToString('N') + '.sql')
    [System.IO.File]::WriteAllText($p, $sql, (New-Object System.Text.UTF8Encoding($false)))
    $r = RunFile $label $p
    Remove-Item $p -ErrorAction SilentlyContinue
    return $r
}
# TWO DUMPS, NEVER ONE. pg_dump lets -t win over -n, so a single invocation
# carrying both wrote a file holding ONLY the registry table while reporting
# success. Each file is size-checked separately.
function DumpPart([string]$label, [string[]]$dumpArgs, [string]$path, [int]$minBytes) {
    & $PgDump -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -w -Fc @dumpArgs -f $path 2>&1 |
        ForEach-Object { W ('           ' + $_) }
    if ($LASTEXITCODE -ne 0) { W ('      FAIL ' + $label + ' - pg_dump exit ' + $LASTEXITCODE); return $false }
    if (-not (Test-Path $path)) { W ('      FAIL ' + $label + ' - no file produced'); return $false }
    $len = (Get-Item $path).Length
    if ($len -lt $minBytes) {
        W ('      FAIL ' + $label + ' - ' + $len + ' bytes is below the ' + $minBytes + ' byte floor;')
        W '           that is the signature of a dump that selected nothing.'
        return $false
    }
    W ('      OK   ' + $label + '   ' + $path + '   ' + [Math]::Round($len / 1MB, 3) + ' MB')
    return $true
}
$Report = Join-Path $EvidenceDir ('T-030_staging_reset_' + $Stamp + '.txt')

W ('PRESENTATION STAGING RESET v2 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('Target   : ' + $TargetDb + '   (ppiq_app guarded)')
W ('Mode     : ' + $(if ($Execute) { 'EXECUTE' } else { 'REPORT ONLY - nothing is changed' }))
W ('Stage 2  : NEVER RUN BY THIS SCRIPT. Canonical is not touched.')
W ('=' * 90)
W ''

W 'PREFLIGHT (fail closed - any FAIL and nothing is changed)'
$pf = 0
function Gate([string]$label, [bool]$ok, [string]$detail) {
    if ($ok) { W ('  OK   ' + $label + '   ' + $detail) } else { W ('  FAIL ' + $label + '   ' + $detail); $script:pf = $script:pf + 1 }
}
$alive = Q1 'SELECT 1;'
Gate 'database reachable' ($alive -eq '1') ''
if ($alive -ne '1') { W ''; W '[ABORT] cannot reach the database.'; Save; exit 1 }
Gate 'dump_store staging schema exists' ((Q1 "SELECT count(*) FROM information_schema.schemata WHERE schema_name='dump_store';") -eq '1') ''

$regRows = Rows @"
SELECT id::text, source_system_code, source_schema_name, source_table_name,
       array_to_string(primary_key_columns, ','), last_index_column,
       dump_schema_name, dump_table_name,
       coalesce(import_cycle_minutes,2)::text, coalesce(hmi_refresh_seconds,30)::text,
       coalesce(last_index_value_text,'<null>')
FROM public.source_table_dump_registry
WHERE is_deleted = false AND is_active = true
ORDER BY source_schema_name, source_table_name;
"@
$reg = @()
foreach ($r in $regRows) {
    $p = $r.Split('|')
    if ($p.Count -lt 11) { continue }
    $reg += [pscustomobject]@{
        Id = $p[0]; Sys = $p[1]; SrcSchema = $p[2]; SrcTable = $p[3]; Pks = $p[4]
        IndexCol = $p[5]; DumpSchema = $p[6]; DumpTable = $p[7]
        Cycle = $p[8]; Refresh = $p[9]; Watermark = $p[10]
    }
}
Gate 'active registry entries resolved' ($reg.Count -gt 0) ('count=' + $reg.Count)
Gate 'registry rows parsed' ($reg.Count -eq $regRows.Count) ('parsed=' + $reg.Count)
Gate 'every active entry targets dump_store' (@($reg | Where-Object { $_.DumpSchema -ne 'dump_store' }).Count -eq 0) ''
Gate 'every active entry sources a src_ donor schema' (@($reg | Where-Object { $_.SrcSchema -notlike 'src_*' }).Count -eq 0) ''
Gate 'no live import lease held' ((Q1 "SELECT count(*) FROM public.source_table_dump_registry WHERE is_deleted=false AND is_active=true AND lease_until_utc IS NOT NULL AND lease_until_utc > now();") -eq '0') ''

$missingSrc = 0; $emptySrc = 0; $missingDump = 0
foreach ($e in $reg) {
    if ((Q1 ("SELECT count(*) FROM information_schema.tables WHERE table_schema='" + $e.SrcSchema + "' AND table_name='" + $e.SrcTable + "';")) -ne '1') { $missingSrc = $missingSrc + 1; continue }
    if ((Q1 ("SELECT count(*) FROM information_schema.tables WHERE table_schema='" + $e.DumpSchema + "' AND table_name='" + $e.DumpTable + "';")) -ne '1') { $missingDump = $missingDump + 1 }
    if ([int](Q1 ('SELECT count(*) FROM "' + $e.SrcSchema + '"."' + $e.SrcTable + '";')) -eq 0) { $emptySrc = $emptySrc + 1 }
}
Gate 'every registered donor table exists' ($missingSrc -eq 0) ('missing=' + $missingSrc)
Gate 'every donor table carries rows' ($emptySrc -eq 0) ('empty=' + $emptySrc)
W ''

# --- SHAPE COMPARISON -------------------------------------------------------
W 'SHAPE - does each dump table still match its donor table'
$stale = @()
foreach ($e in $reg) {
    $missingCols = Q1 ("SELECT count(*) FROM (SELECT column_name FROM information_schema.columns WHERE table_schema='" + $e.SrcSchema + "' AND table_name='" + $e.SrcTable + "' EXCEPT SELECT column_name FROM information_schema.columns WHERE table_schema='" + $e.DumpSchema + "' AND table_name='" + $e.DumpTable + "') q;")
    $dumpExists = (Q1 ("SELECT count(*) FROM information_schema.tables WHERE table_schema='" + $e.DumpSchema + "' AND table_name='" + $e.DumpTable + "';")) -eq '1'
    if ((-not $dumpExists) -or ($null -eq $missingCols) -or ([int]$missingCols -gt 0)) {
        $stale += $e
        W ('  STALE   ' + ($e.SrcSchema + '.' + $e.SrcTable).PadRight(52) + 'donor columns absent from the dump table: ' + $(if ($dumpExists) { $missingCols } else { 'table missing' }))
    } else {
        W ('  OK      ' + ($e.SrcSchema + '.' + $e.SrcTable).PadRight(52) + 'shape matches')
    }
}
W ('  ' + $stale.Count + ' of ' + $reg.Count + ' dump tables need rebuilding')
W ''

W 'CURRENT STATE - donor versus staging, per registered table'
W ('  ' + 'donor table'.PadRight(52) + 'donor'.PadLeft(9) + 'staging'.PadLeft(11) + '   watermark')
foreach ($e in $reg) {
    $sc = Q1 ('SELECT count(*) FROM "' + $e.SrcSchema + '"."' + $e.SrcTable + '";')
    $dc = Q1 ('SELECT count(*) FROM "' + $e.DumpSchema + '"."' + $e.DumpTable + '";')
    if ($null -eq $sc) { $sc = '0' }
    if ($null -eq $dc) { $dc = 'n/a' }
    W ('  ' + ($e.SrcSchema + '.' + $e.SrcTable).PadRight(52) + $sc.PadLeft(9) + $dc.PadLeft(11) + '   ' + $e.Watermark)
}
W ''
if ($pf -gt 0) { W ('[ABORT] preflight failed with ' + $pf + ' error(s). NOTHING WAS CHANGED.'); Save; exit 2 }

if (-not $Execute) {
    W 'REPORT ONLY. Nothing was changed.'
    W ''
    W 'With -Execute this script would, in order:'
    W ('  1  TWO pg_dump files into ' + $BackupDir + ' - the dump_store schema, and the registry - each size-checked')
    W ('  2  drop and re-register the ' + $stale.Count + ' stale dump table(s) through ppiq_register_dump_source')
    W '  3  clear the population and null every stage-1 watermark, in one transaction'
    W '  4  run public.ppiq_run_stage1_delta_import_all - a FULL load, because a null watermark makes the predicate TRUE'
    W '  5  verify staging equals the donor per table, and the identity checks'
    W ''
    W 'Stage 2 is not run. Canonical is not touched.'
    W ('Report: ' + $Report)
    Save
    exit 0
}

W '[1/5] backup, in two dumps'
$bdir = Join-Path $RepoRoot $BackupDir
if (-not (Test-Path $bdir)) { New-Item -ItemType Directory -Path $bdir -Force | Out-Null }
$bStg = Join-Path $bdir ('ppiq_presentation_dumpstore_' + $Stamp + '.dump')
$bReg = Join-Path $bdir ('ppiq_presentation_registry_' + $Stamp + '.dump')
$stgRows = Q1 @"
SELECT coalesce(sum(n),0) FROM (
  SELECT (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', table_schema, table_name), false, true, '')))[1]::text::bigint AS n
  FROM information_schema.tables WHERE table_schema='dump_store' AND table_type='BASE TABLE') q;
"@
$floor = 1000
if ($null -ne $stgRows -and [int64]$stgRows -gt 0) { $floor = 200000 }
W ('      staging rows to preserve : ' + $stgRows + '   (size floor ' + $floor + ' bytes)')
$ok1 = DumpPart 'dump_store schema' @('-n', 'dump_store') $bStg $floor
$ok2 = DumpPart 'dump registry' @('-t', 'public.source_table_dump_registry') $bReg 1000
if (-not ($ok1 -and $ok2)) { W '[ABORT] backup failed its own size check. NOTHING WAS CHANGED.'; Save; exit 3 }
W ''

W '[2/5] rebuild stale dump tables through the existing ppiq_register_dump_source'
if ($stale.Count -eq 0) { W '      none stale; nothing to rebuild' } else {
    $sql = "BEGIN;`r`n"
    foreach ($e in $stale) { $sql = $sql + 'DROP TABLE IF EXISTS "' + $e.DumpSchema + '"."' + $e.DumpTable + '" CASCADE;' + "`r`n" }
    foreach ($e in $stale) {
        $pkList = ($e.Pks.Split(',') | ForEach-Object { "'" + $_.Trim() + "'" }) -join ','
        $sql = $sql + "SELECT public.ppiq_register_dump_source('" + $e.Sys + "', '" + $e.SrcSchema + "', '" + $e.SrcTable + "', ARRAY[" + $pkList + "], '" + $e.IndexCol + "', " + $e.Cycle + ", " + $e.Refresh + ");`r`n"
    }
    $sql = $sql + "COMMIT;`r`n"
    if (-not (RunSql ('rebuild ' + $stale.Count + ' dump table(s)') $sql)) {
        W '[ABORT] rebuild failed.'
        W ('        restore with: pg_restore -h ' + $DbHost + ' -U ' + $DbUser + ' -d ' + $TargetDb + ' --clean "' + $bStg + '"')
        Save; exit 4
    }
}
W ''

W '[3/5] clear the population and reset stage-1 watermarks (single transaction)'
$truncs = ''
foreach ($e in $reg) { $truncs = $truncs + 'TRUNCATE TABLE "' + $e.DumpSchema + '"."' + $e.DumpTable + '";' + "`r`n" }
$resetSql = "BEGIN;`r`n" + $truncs + @"
UPDATE public.source_table_dump_registry
SET updated_at_utc = now(), last_index_value_text = NULL, stage1_status = 'Pending',
    last_stage1_run_id = NULL, last_stage1_started_at_utc = NULL,
    last_stage1_completed_at_utc = NULL, last_stage1_duration_ms = 0,
    last_stage1_inserted_rows = 0, lease_owner = NULL, lease_until_utc = NULL, last_error = NULL
WHERE is_deleted = false AND is_active = true AND dump_schema_name = 'dump_store';
COMMIT;
"@
if (-not (RunSql 'truncate + watermark reset' $resetSql)) {
    W '[ABORT] the reset transaction failed and rolled back.'
    W ('        restore with: pg_restore -h ' + $DbHost + ' -U ' + $DbUser + ' -d ' + $TargetDb + ' --clean "' + $bStg + '"')
    Save; exit 5
}
$notEmpty = 0
foreach ($e in $reg) { if ([int](Q1 ('SELECT count(*) FROM "' + $e.DumpSchema + '"."' + $e.DumpTable + '";')) -ne 0) { $notEmpty = $notEmpty + 1 } }
if ($notEmpty -gt 0) { W ('[ABORT] ' + $notEmpty + ' dump table(s) did not clear.'); Save; exit 6 }
W '      OK   every registered dump table is empty'
W ''

W '[4/5] full load via public.ppiq_run_stage1_delta_import_all (null watermark => full copy)'
$loadRows = Rows @"
SELECT registry_id::text, status, inserted_rows::text, coalesce(message,'')
FROM public.ppiq_run_stage1_delta_import_all('t031-staging-reset', 400000, 1800);
"@
foreach ($r in $loadRows) { W ('      ' + $r) }
$bad = @($loadRows | Where-Object { $_ -notmatch '\|Ok\|' })
if ($bad.Count -gt 0) {
    W ('[ABORT] ' + $bad.Count + ' stage-1 run(s) did not return Ok.')
    W ('        restore with: pg_restore -h ' + $DbHost + ' -U ' + $DbUser + ' -d ' + $TargetDb + ' --clean "' + $bStg + '"')
    Save; exit 7
}
W ''

W '[5/5] verify and gate'
$mismatch = 0
W ('  ' + 'table'.PadRight(52) + 'donor'.PadLeft(9) + 'staging'.PadLeft(11) + '   verdict')
foreach ($e in $reg) {
    $sc = Q1 ('SELECT count(*) FROM "' + $e.SrcSchema + '"."' + $e.SrcTable + '";')
    $dc = Q1 ('SELECT count(*) FROM "' + $e.DumpSchema + '"."' + $e.DumpTable + '";')
    $v = 'MATCH'
    if ([int]$sc -ne [int]$dc) { $v = 'MISMATCH'; $mismatch = $mismatch + 1 }
    W ('  ' + ($e.SrcSchema + '.' + $e.SrcTable).PadRight(52) + $sc.PadLeft(9) + $dc.PadLeft(11) + '   ' + $v)
}
W ''
$stale2 = Q1 @"
SELECT count(*) FROM dump_store.src_hsm_oracle_shape_hsm_coils d
WHERE NOT EXISTS (SELECT 1 FROM src_hsm_oracle_shape.hsm_coils s WHERE s.coil_id = d.coil_id);
"@
$noCanon = Q1 @"
SELECT count(*) FROM dump_store.src_hsm_oracle_shape_hsm_coils d
WHERE NOT EXISTS (SELECT 1 FROM material_units m WHERE m.material_code = d.coil_id AND coalesce(m.is_deleted,false)=false);
"@
$noStage = Q1 @"
SELECT count(*) FROM material_units m WHERE coalesce(m.is_deleted,false)=false AND m.material_unit_type='Coil'
  AND NOT EXISTS (SELECT 1 FROM dump_store.src_hsm_oracle_shape_hsm_coils d WHERE d.coil_id = m.material_code);
"@
W ('      staging coils absent from the donor        : ' + $stale2)
W ('      staging coils absent from canonical        : ' + $noCanon)
W ('      canonical coils absent from staging        : ' + $noStage)
W ''
$gateFail = 0
if ($mismatch -ne 0) { W ('      FAIL ' + $mismatch + ' table(s) do not match the donor row for row'); $gateFail = $gateFail + 1 } else { W '      OK   every registered table matches the donor exactly' }
if ($stale2 -ne '0') { W ('      FAIL ' + $stale2 + ' staging identities are absent from the donor'); $gateFail = $gateFail + 1 } else { W '      OK   zero stale staging identities' }
if ($noCanon -ne '0') { W ('      FAIL ' + $noCanon + ' staging coils have no canonical match'); $gateFail = $gateFail + 1 } else { W '      OK   every staging coil resolves in canonical' }
if ($noStage -ne '0') { W ('      NOTE ' + $noStage + ' canonical coils are absent from staging - the certification judges this') } else { W '      OK   every canonical coil is present in staging' }
W ''
if ($gateFail -gt 0) {
    W '[GATE RED] staging was NOT accepted.'
    W ('           restore with: pg_restore -h ' + $DbHost + ' -U ' + $DbUser + ' -d ' + $TargetDb + ' --clean "' + $bStg + '"')
    Save; exit 8
}
W '[GATE GREEN] dump_store carries the current donor population.'
W '             Stage 2 was NOT run. Canonical and the analytical population are untouched.'
W ('             Backups: ' + $bStg)
W ('                      ' + $bReg)
W ('             Report:  ' + $Report)
Save
exit 0