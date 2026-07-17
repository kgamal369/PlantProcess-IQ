# ============================================================================
# Trap-PresentationWipe.ps1  v1.1   EVIDENCE BEFORE CURE
# (v1.1: off-database OID tripwire catches DROP DATABASE; live counts; fixes)
#
# THE FACTS: ppiq_presentation has been destroyed twice in 15 hours.
#   16-Jul 20:24  40,148 units verified
#   17-Jul 09:36  4 units          (overnight, actor unknown)
#   17-Jul 10:22  40,148 rebuilt + verified
#   17-Jul 10:54  API started (commit bebc8b23)
#   17-Jul 11:07  login fails - the users table is gone too
#   17-Jul 11:08  4 units          (45-minute window, actor unknown)
# Nothing you or I ran in that window deletes rows. Something else does.
#
# THIS SCRIPT DOES NOT GUESS. It arms three traps and waits:
#   1. ppiq_forensics.wipe_audit   - audit table in a SEPARATE schema, so
#                                    pg_restore --clean does NOT drop it and
#                                    the evidence survives the next rebuild
#   2. statement triggers          - BEFORE DELETE OR TRUNCATE on the core
#                                    tables; records current_query(),
#                                    application_name, client_addr,
#                                    session_user, backend PID, rows_before
#   3. sql_drop event trigger      - catches DROP TABLE / schema drops that a
#                                    row trigger would never see
#   ...plus optional server-side statement logging for the same database.
#
# THE TRIGGERS LIVE ON public TABLES, so a rebuild removes them: RE-ARM AFTER
# EVERY Rebuild-PresentationDb RUN. The audit table and its history survive.
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Trap-PresentationWipe.ps1 -Arm
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Trap-PresentationWipe.ps1 -Report
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Trap-PresentationWipe.ps1 -Disarm
# ============================================================================
[CmdletBinding()]
param(
    [switch]$Arm,
    [switch]$Report,
    [switch]$Disarm,
    [switch]$EnableServerLogging,
    [string]$TargetDb = 'ppiq_presentation'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'
if ($TargetDb -notmatch 'presentation|app') { Write-Host "[REFUSED] guard." -ForegroundColor Red; exit 1 }

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$Out = Join-Path $RepoRoot ('WipeTrap_' + $Stamp + '.txt')
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

function RunSql([string]$label, [string]$sql) {
    $tmp = Join-Path $env:TEMP ("ppiq_trap_" + [guid]::NewGuid().ToString('N') + ".sql")
    [System.IO.File]::WriteAllText($tmp, $sql, (New-Object System.Text.UTF8Encoding($false)))
    $o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -v ON_ERROR_STOP=1 -f $tmp 2>&1
    $code = $LASTEXITCODE
    Remove-Item $tmp -ErrorAction SilentlyContinue
    if ($code -eq 0) { W ("    OK   " + $label) } else {
        W ("    FAIL " + $label)
        @($o | Select-Object -First 4) | ForEach-Object { W ("         " + $_) }
    }
    return ($code -eq 0)
}
function Rows([string]$q) {
    return @(& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F ' | ' -c $q 2>&1 | Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}

W ("PRESENTATION WIPE TRAP - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + "   DB: " + $TargetDb)
W ("=" * 78)
W ""

$TRAPPED = @('material_units', 'quality_events', 'genealogy_edges', 'parameter_observations', 'staging_records', 'import_batches')

# ---------------------------------------------------------------- REPORT
if ($Report) {
    # live health first - so "0 events" is interpretable
    W "[REPORT] live table state:"
    foreach ($t in @('material_units','quality_events','genealogy_edges')) {
        $n = @(Rows ("SELECT COUNT(*) FROM public." + $t + ";"))
        $v = 'ABSENT'
        if ($n.Count -gt 0) { $v = ([string]$n[0]).Trim() }
        W ("    " + $t.PadRight(24) + " " + $v)
    }
    W ""
    $trapState = Join-Path $RepoRoot 'wipetrap_state.json'
    if (Test-Path $trapState) {
        $st = Get-Content $trapState -Raw | ConvertFrom-Json
        $curOid = (Rows ("SELECT oid FROM pg_database WHERE datname='" + $TargetDb + "';") | Select-Object -First 1)
        W ("[TRIPWIRE] db oid at arm: " + $st.dbOid + "   now: " + $curOid)
        if ([string]$curOid -ne [string]$st.dbOid) {
            W "    ******************************************************************"
            W "    VERDICT: THE DATABASE WAS DROPPED AND RECREATED since arming."
            W "    Only DROP DATABASE + CREATE DATABASE changes the oid. No"
            W "    in-database trap survives it - that is why wipe_audit may be"
            W "    empty or missing below. THE ACTOR is whatever runs"
            W ("    CREATE DATABASE " + $TargetDb + ": a rebuild/provisioning script,")
            W "    a test harness that recreates its target, or a CI pipeline."
            W "    Search those for DROP DATABASE / CREATE DATABASE / template."
            W "    ******************************************************************"
        } else {
            W "    oid unchanged - the database itself has NOT been recreated since arming."
        }
    } else {
        W "[TRIPWIRE] no wipetrap_state.json - arming with v1.1 records it."
    }
    W ""
    W "[REPORT] forensic audit contents:"
    $exists = Rows "SELECT to_regclass('ppiq_forensics.wipe_audit')::text;"
    if (@($exists).Count -eq 0 -or -not $exists[0]) {
        W "    audit table does not exist. Two possible reasons:"
        W "      a) the trap was never armed on THIS incarnation of the database"
        W "      b) the DATABASE WAS DROPPED AND RECREATED (see tripwire above -"
        W "         a changed oid makes this certain)"
        W "    Re-arm now: .\Trap-PresentationWipe.ps1 -Arm"
        Save; exit 1
    }
    $n = Rows "SELECT COUNT(*) FROM ppiq_forensics.wipe_audit;"
    W ("    events recorded: " + $n[0])
    W ""
    if ([int]$n[0] -eq 0) {
        W "    NOTHING CAUGHT YET. Leave the trap armed and keep working."
        W "    When the DB empties again, run -Report immediately."
    } else {
        W "    ---- the evidence ----"
        Rows @"
SELECT to_char(occurred_at,'YYYY-MM-DD HH24:MI:SS') || '  ' || table_name || '  ' || operation ||
       '  rows_before=' || COALESCE(rows_before::text,'?') ||
       E'\n        app=' || COALESCE(application_name,'(none)') ||
       '  user=' || COALESCE(session_user_name,'?') ||
       '  pid=' || COALESCE(backend_pid::text,'?') ||
       '  addr=' || COALESCE(client_addr,'local') ||
       E'\n        query: ' || left(COALESCE(query,'(null)'), 300)
FROM ppiq_forensics.wipe_audit ORDER BY occurred_at DESC LIMIT 25;
"@ | ForEach-Object { W ("    " + $_) }
        W ""
        W "    ---- summary by actor ----"
        Rows "SELECT COALESCE(application_name,'(none)') || ' | ' || COALESCE(session_user_name,'?') || ' | ' || COUNT(*) || ' event(s)' FROM ppiq_forensics.wipe_audit GROUP BY 1,2 ORDER BY COUNT(*) DESC;" | ForEach-Object { W ("    " + $_) }
        W ""
        W "    READ THE application_name. That names the process. A .NET app sets it"
        W "    from the connection string; test runners usually leave it as the exe."
    }
    Save
    Write-Host ""
    Write-Host ("[DONE] -> " + $Out) -ForegroundColor Green
    exit 0
}

# ---------------------------------------------------------------- DISARM
if ($Disarm) {
    W "[DISARM] removing triggers (audit history is KEPT):"
    foreach ($t in $TRAPPED) {
        [void](RunSql ("drop trigger on " + $t) ("DROP TRIGGER IF EXISTS ppiq_wipe_trap_" + $t + " ON public." + $t + ";"))
    }
    [void](RunSql 'drop event trigger' "DROP EVENT TRIGGER IF EXISTS ppiq_wipe_trap_ddl;")
    W ""
    W "History remains in ppiq_forensics.wipe_audit. Re-arm any time."
    Save; exit 0
}

if (-not $Arm) {
    W "Nothing to do. Use -Arm, -Report, or -Disarm."
    W ""
    W "TYPICAL USE:"
    W "  1. .\Rebuild-PresentationDb.ps1 -Execute"
    W "  2. .\Trap-PresentationWipe.ps1 -Arm        <- re-arm after EVERY rebuild"
    W "  3. ...work normally..."
    W "  4. the moment the DB looks empty: .\Trap-PresentationWipe.ps1 -Report"
    Save; exit 0
}

# ---------------------------------------------------------------- ARM
# off-database tripwire: the DB OID changes if anyone DROPs+recreates the DB.
$dbOid = (Rows ("SELECT oid FROM pg_database WHERE datname='" + $TargetDb + "';") | Select-Object -First 1)
$trapState = Join-Path $RepoRoot 'wipetrap_state.json'
@{ armedAt = (Get-Date).ToString('o'); db = $TargetDb; dbOid = [string]$dbOid } | ConvertTo-Json | Out-File -FilePath $trapState -Encoding utf8
W ("[ARM] 0. off-database tripwire: db oid " + $dbOid + " recorded -> wipetrap_state.json")
W "      (a changed oid at -Report time = the DATABASE ITSELF was dropped and recreated)"
W ""
W "[ARM] 1. forensic schema (survives pg_restore --clean):"
[void](RunSql 'ppiq_forensics.wipe_audit' @"
CREATE SCHEMA IF NOT EXISTS ppiq_forensics;
CREATE TABLE IF NOT EXISTS ppiq_forensics.wipe_audit (
    id                bigserial PRIMARY KEY,
    occurred_at       timestamptz NOT NULL DEFAULT now(),
    table_name        text,
    operation         text,
    rows_before       bigint,
    query             text,
    application_name  text,
    client_addr       text,
    session_user_name text,
    backend_pid       integer
);
"@)

W "[ARM] 2. audit function + statement triggers:"
[void](RunSql 'ppiq_forensics.audit_wipe()' @"
CREATE OR REPLACE FUNCTION ppiq_forensics.audit_wipe() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER AS `$fn`$
DECLARE n bigint;
BEGIN
    BEGIN
        EXECUTE format('SELECT count(*) FROM %I.%I', TG_TABLE_SCHEMA, TG_TABLE_NAME) INTO n;
    EXCEPTION WHEN OTHERS THEN n := NULL;
    END;
    INSERT INTO ppiq_forensics.wipe_audit
        (table_name, operation, rows_before, query, application_name, client_addr, session_user_name, backend_pid)
    VALUES
        (TG_TABLE_NAME, TG_OP, n, current_query(),
         current_setting('application_name', true), inet_client_addr()::text,
         session_user, pg_backend_pid());
    RETURN NULL;
END;
`$fn`$;
"@)
foreach ($t in $TRAPPED) {
    [void](RunSql ("trigger on " + $t) (@"
DROP TRIGGER IF EXISTS ppiq_wipe_trap_$t ON public.$t;
CREATE TRIGGER ppiq_wipe_trap_$t
    BEFORE DELETE OR TRUNCATE ON public.$t
    FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();
"@))
}

W "[ARM] 3. DDL drop trap (catches DROP TABLE, which row triggers never see):"
[void](RunSql 'sql_drop event trigger' @"
CREATE OR REPLACE FUNCTION ppiq_forensics.audit_ddl() RETURNS event_trigger
LANGUAGE plpgsql AS `$fn`$
DECLARE r record;
BEGIN
    FOR r IN SELECT * FROM pg_event_trigger_dropped_objects() LOOP
        IF r.object_type IN ('table','schema') AND r.schema_name = 'public' THEN
            INSERT INTO ppiq_forensics.wipe_audit
                (table_name, operation, query, application_name, client_addr, session_user_name, backend_pid)
            VALUES
                (r.object_identity, 'DROP:' || r.object_type, current_query(),
                 current_setting('application_name', true), inet_client_addr()::text,
                 session_user, pg_backend_pid());
        END IF;
    END LOOP;
END;
`$fn`$;
DROP EVENT TRIGGER IF EXISTS ppiq_wipe_trap_ddl;
CREATE EVENT TRIGGER ppiq_wipe_trap_ddl ON sql_drop EXECUTE FUNCTION ppiq_forensics.audit_ddl();
"@)

if ($EnableServerLogging) {
    W "[ARM] 4. server-side statement logging for this database:"
    [void](RunSql 'log_statement=mod' ("ALTER DATABASE " + $TargetDb + " SET log_statement='mod';"))
    $ld = Rows "SELECT setting FROM pg_settings WHERE name='log_directory';"
    $dd = Rows "SELECT setting FROM pg_settings WHERE name='data_directory';"
    W ("      log_directory : " + $(if (@($ld).Count) { $ld[0] } else { '?' }))
    W ("      data_directory: " + $(if (@($dd).Count) { $dd[0] } else { '?' }))
    W "      (applies to NEW connections - restart the API to pick it up)"
} else {
    W "[ARM] 4. server-side logging skipped (-EnableServerLogging to add it)"
}

W ""
W "---- armed on ----"
Rows "SELECT c.relname || '  ->  ' || t.tgname FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid WHERE t.tgname LIKE 'ppiq_wipe_trap%' ORDER BY 1;" | ForEach-Object { W ("    " + $_) }
Rows "SELECT evtname || ' (event trigger, ' || evtevent || ')' FROM pg_event_trigger WHERE evtname LIKE 'ppiq_wipe%';" | ForEach-Object { W ("    " + $_) }
W ""
W "=" * 78
W "TRAP ARMED. Now work normally. The instant the database looks empty:"
W "    powershell -NoProfile -ExecutionPolicy Bypass -File .\Trap-PresentationWipe.ps1 -Report"
W ""
W "It will name the query, the application, the user and the PID that did it."
W "RE-ARM AFTER EVERY REBUILD - pg_restore --clean drops the public triggers."
W "(The audit table is in ppiq_forensics and survives.)"
Save
Write-Host ""
Write-Host ("[DONE] -> " + $Out) -ForegroundColor Green
exit 0
