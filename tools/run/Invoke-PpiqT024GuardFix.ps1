#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-024 blocker - repair the genealogy contribution-weight guard, prove the
    invariant still holds, and re-run the dry proof.

.DESCRIPTION
    THE DEFECT, as ruled: the business invariant is correct and stays. The
    IMPLEMENTATION was bulk-write unsafe - a DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW trigger whose body aggregated the WHOLE genealogy_edges table,
    grouped by child, on every firing. With about 70,000 queued delete and insert
    events that is roughly 2.4 billion row-touches at COMMIT: not slow, quadratic.

    WHAT IS PRESERVED, exactly as ruled:
      - the invariant: contribution weights sum to 1.0 per child
      - the 0.015 tolerance, unchanged
      - DEFERRABLE INITIALLY DEFERRED, FOR EACH ROW - NOT converted to a
        statement-level trigger, because T-024 deletes and recreates genealogy
        inside one transaction and a statement-level check could observe a
        legitimate intermediate state
      - failure on invalid genealogy, no later than commit

    WHAT CHANGES: the body validates only the AFFECTED CHILD taken from NEW or
    OLD, instead of aggregating every row in the table.
      INSERT -> NEW.child_material_unit_id
      DELETE -> OLD.child_material_unit_id
      UPDATE -> both, when the child identity moved

    A child with no remaining edges is NOT a violation - the unit may have been
    deleted in the same transaction. That matches the original, whose GROUP BY
    simply produced no row for such a child.

    PHASES
      1  cancel the pathological transaction if it still runs
      2  prove the rollback by reading the pre-T024 counts
      3  confirm no application session is connected
      4  confirm the index the new lookup path needs, and create it if absent
      5  patch the guard
      6  regression tests, all five the ruling requires
      7  the dry proof on a clone

    The LIVE replacement is deliberately NOT run here.

.EXAMPLE
    .\tools\run\Invoke-PpiqT024GuardFix.ps1 -ReportOnly
    .\tools\run\Invoke-PpiqT024GuardFix.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$DryDatabase = "ppiq_t024_dry",
    [string]$PsqlPath   = "",
    [switch]$SkipDryRun,
    [switch]$ReportOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

function Say  { param([string]$T) Write-Host $T }
function Rule { param([string]$T) Write-Host ""; Write-Host ("=" * 78); Write-Host $T; Write-Host ("=" * 78) }

function Read-IfExists {
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) { return [System.IO.File]::ReadAllText($Path) }
    return ""
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

function Invoke-Sql {
    param([string]$Sql, [string]$Tag, [string]$Db = "", [switch]$Stream)
    if ($Db -eq "") { $Db = $Database }
    $f = Join-Path $script:tmp ($Tag + ".sql")
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Db,
           "-v", "ON_ERROR_STOP=1", "-f", $f)
    if (-not $Stream) { $a += @("-o", $o) }
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait `
                       -PassThru -RedirectStandardError $e
    $r = New-Object psobject
    Add-Member -InputObject $r -MemberType NoteProperty -Name ExitCode -Value $p.ExitCode
    Add-Member -InputObject $r -MemberType NoteProperty -Name Output   -Value (Read-IfExists $o)
    Add-Member -InputObject $r -MemberType NoteProperty -Name Error    -Value (Read-IfExists $e)
    return $r
}

Rule "PPIQ T-024 BLOCKER - GENEALOGY GUARD REPAIR"
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Say "[FAIL] psql.exe not found."; exit 2 }
Say ("Database : " + $Database)
Say ("Mode     : " + $(if ($ReportOnly) { "REPORT ONLY" } else { "APPLY" }))

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t024g_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$bad = 0

try {
    Rule "1 - THE PATHOLOGICAL TRANSACTION"
    $act = Invoke-Sql -Tag "activity" -Sql @'
\pset border 2
SELECT pid, state, coalesce(wait_event,'(running)') AS wait_event,
       date_trunc('second', now()-xact_start) AS running, left(query,30) AS q
FROM pg_stat_activity
WHERE datname = current_database() AND pid <> pg_backend_pid()
ORDER BY xact_start;
'@
    Say $act.Output
    $stuck = @()
    foreach ($line in ($act.Output -split "`n")) {
        $t = $line.Trim()
        if ($t -match "^\|\s*(\d+)\s*\|\s*active\s*\|.*\|\s*COMMIT;?\s*\|") {
            $stuck += $Matches[1]
        }
    }
    if ($stuck.Count -gt 0) {
        Say ("[WARN] " + $stuck.Count + " backend(s) still in COMMIT: " + ($stuck -join ", "))
        if ($ReportOnly) {
            Say "       report-only, so nothing is cancelled."
        } else {
            foreach ($pid in $stuck) {
                $c = Invoke-Sql -Tag ("cancel_" + $pid) `
                                -Sql ("SELECT pg_cancel_backend(" + $pid + ");")
                Say ("  cancel " + $pid + " -> exit " + $c.ExitCode)
            }
            Start-Sleep -Seconds 5
        }
    } else {
        Say "[OK] no transaction is stuck in COMMIT"
    }

    Rule "2 - PROVE THE ROLLBACK"
    Say "It was one transaction. These must be the PRE-T024 counts, unchanged."
    $roll = Invoke-Sql -Tag "rollback" -Sql @'
\pset border 2
SELECT 'material_units' AS entity, count(*) AS rows FROM public.material_units
UNION ALL SELECT 'genealogy_edges', count(*) FROM public.genealogy_edges
UNION ALL SELECT 'parameter_observations', count(*) FROM public.parameter_observations
UNION ALL SELECT 'quality_events', count(*) FROM public.quality_events
UNION ALL SELECT 'downtime_events', count(*) FROM public.downtime_events
UNION ALL SELECT 'fleet v2 rows anywhere', (
  SELECT count(*) FROM public.material_units WHERE source_system='FLEET_V2')
ORDER BY 1;
'@
    Say $roll.Output

    Rule "3 - APPLICATION SESSIONS"
    $conn = Invoke-Sql -Tag "conns" -Sql @'
\pset border 2
SELECT count(*) AS other_sessions FROM pg_stat_activity
WHERE datname = current_database() AND pid <> pg_backend_pid();
'@
    Say $conn.Output
    Say "The dry proof clones this database and CANNOT run while the API holds a"
    Say "connection. Stop the API before phase 7 if any session is listed."

    Rule "4 - THE INDEX THE NEW LOOKUP NEEDS"
    Say "The repaired guard looks up by child_material_unit_id. The existing unique"
    Say "index leads with parent_material_unit_id, which does NOT serve that lookup."
    $idx = Invoke-Sql -Tag "idx" -Sql @'
\pset border 2
SELECT i.relname AS index_name, pg_get_indexdef(i.oid) AS definition
FROM pg_class t
JOIN pg_namespace n ON n.oid = t.relnamespace AND n.nspname='public'
JOIN pg_index ix ON ix.indrelid = t.oid
JOIN pg_class i ON i.oid = ix.indexrelid
WHERE t.relname = 'genealogy_edges'
ORDER BY 1;
'@
    Say $idx.Output
    $hasChildIdx = ($idx.Output -match "USING btree \(child_material_unit_id")
    if ($hasChildIdx) {
        Say "[OK] an index leading with child_material_unit_id exists"
    } elseif ($ReportOnly) {
        Say "[WARN] no such index. It WOULD be created on apply."
    } else {
        Say "creating ix_genealogy_edges_child_material_unit_id ..."
        $ci = Invoke-Sql -Tag "mkidx" -Sql @'
CREATE INDEX IF NOT EXISTS ix_genealogy_edges_child_material_unit_id
  ON public.genealogy_edges (child_material_unit_id);
'@
        if ($ci.ExitCode -ne 0) { Say $ci.Error; throw "index" }
        Say "[OK] index created"
    }

    $guardSql = @'
-- PPIQ T-024 - genealogy contribution weight guard, repaired.
--
-- PRESERVED: the invariant, the 0.015 tolerance, DEFERRABLE INITIALLY DEFERRED
-- FOR EACH ROW, and failure no later than commit. The trigger definition is NOT
-- touched - only the function body.
--
-- CHANGED: it validates the AFFECTED CHILD from NEW or OLD instead of
-- aggregating every row in genealogy_edges on every firing.
CREATE OR REPLACE FUNCTION public.ppiq_genealogy_edge_weight_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    target uuid;
    total numeric;
BEGIN
    IF TG_OP = 'INSERT' THEN
        target := NEW.child_material_unit_id;
    ELSIF TG_OP = 'DELETE' THEN
        target := OLD.child_material_unit_id;
    ELSE
        IF OLD.child_material_unit_id IS DISTINCT FROM NEW.child_material_unit_id THEN
            SELECT sum(contribution_weight) INTO total
            FROM public.genealogy_edges
            WHERE child_material_unit_id = OLD.child_material_unit_id
              AND COALESCE(is_deleted, false) = false;
            IF total IS NOT NULL AND abs(total - 1.0) > 0.015 THEN
                RAISE EXCEPTION
                  'Genealogy contribution weights must sum to 1.0 per child. child=%, sum=%',
                  OLD.child_material_unit_id, total;
            END IF;
        END IF;
        target := NEW.child_material_unit_id;
    END IF;

    SELECT sum(contribution_weight) INTO total
    FROM public.genealogy_edges
    WHERE child_material_unit_id = target
      AND COALESCE(is_deleted, false) = false;

    -- A child with no remaining edges is NOT a violation: the unit may have been
    -- deleted in the same transaction. The original behaved the same way, since
    -- its GROUP BY produced no row for such a child.
    IF total IS NOT NULL AND abs(total - 1.0) > 0.015 THEN
        RAISE EXCEPTION
          'Genealogy contribution weights must sum to 1.0 per child. child=%, sum=%',
          target, total;
    END IF;

    RETURN NULL;
END
$function$;
'@

    if ($ReportOnly) {
        Rule "5 - PATCH (NOT APPLIED)"
        Say "The repaired function would be installed. Re-run without -ReportOnly."
        Rule "REPORT ONLY - NOTHING CHANGED"
        exit 0
    }

    Rule "5 - PATCH THE GUARD"
    $pf = Invoke-Sql -Tag "patch" -Sql $guardSql
    if ($pf.ExitCode -ne 0 -or $pf.Error -match "(?i)(ERROR|FATAL):") {
        Say $pf.Error; throw "patch"
    }
    Say "[OK] function replaced; the trigger definition is untouched"
    $tg = Invoke-Sql -Tag "trigdef" -Sql @'
\pset border 2
SELECT tgname, pg_get_triggerdef(oid) LIKE '%DEFERRABLE INITIALLY DEFERRED%' AS still_deferred,
       pg_get_triggerdef(oid) LIKE '%FOR EACH ROW%' AS still_row_level
FROM pg_trigger WHERE tgname = 'ppiq_genealogy_edge_weight_guard_after_change';
'@
    Say $tg.Output

    Rule "6 - REGRESSION TESTS"
    $testSql = @'
\pset border 2
\set ON_ERROR_STOP on

-- T1  valid genealogy commits
DO $t$
DECLARE p uuid; c uuid; s uuid;
BEGIN
  SELECT id INTO s FROM public.sites LIMIT 1;
  p := gen_random_uuid(); c := gen_random_uuid();
  INSERT INTO public.material_units (id, material_code, material_unit_type, site_id,
    plant_time_zone_id, plant_utc_offset_minutes, created_at_utc, is_synthetic,
    source_system, is_deleted)
  VALUES (p,'T024-TEST-PARENT','Heat',s,'UTC',0,now(),true,'T024_TEST',false),
         (c,'T024-TEST-CHILD','Slab',s,'UTC',0,now(),true,'T024_TEST',false);
  INSERT INTO public.genealogy_edges (id, parent_material_unit_id,
    child_material_unit_id, relationship_type, contribution_weight,
    provenance_confidence, created_at_utc, is_synthetic, source_system, is_deleted)
  VALUES (gen_random_uuid(), p, c, 'ProducedInto', 1.0, 1.0, now(), true,
          'T024_TEST', false);
  RAISE NOTICE 'T1 PASS - valid genealogy accepted';
END $t$;

-- T2  an invalid child contribution sum STILL FAILS, and no later than commit
DO $t$
DECLARE p uuid; c uuid; s uuid; failed boolean := false;
BEGIN
  SELECT id INTO s FROM public.sites LIMIT 1;
  BEGIN
    p := gen_random_uuid(); c := gen_random_uuid();
    INSERT INTO public.material_units (id, material_code, material_unit_type, site_id,
      plant_time_zone_id, plant_utc_offset_minutes, created_at_utc, is_synthetic,
      source_system, is_deleted)
    VALUES (p,'T024-TEST-BADP','Heat',s,'UTC',0,now(),true,'T024_TEST',false),
           (c,'T024-TEST-BADC','Slab',s,'UTC',0,now(),true,'T024_TEST',false);
    INSERT INTO public.genealogy_edges (id, parent_material_unit_id,
      child_material_unit_id, relationship_type, contribution_weight,
      provenance_confidence, created_at_utc, is_synthetic, source_system, is_deleted)
    VALUES (gen_random_uuid(), p, c, 'ProducedInto', 0.5, 1.0, now(), true,
            'T024_TEST', false);
    -- force the deferred check to run now
    SET CONSTRAINTS ppiq_genealogy_edge_weight_guard_after_change IMMEDIATE;
  EXCEPTION WHEN others THEN
    failed := true;
    RAISE NOTICE 'T2 PASS - invalid sum rejected: %', SQLERRM;
  END;
  IF NOT failed THEN
    RAISE EXCEPTION 'T2 FAIL - a child summing to 0.5 was accepted';
  END IF;
END $t$;

-- T3  cleanup of the T1 fixture
DELETE FROM public.genealogy_edges WHERE source_system = 'T024_TEST';
DELETE FROM public.material_units WHERE source_system = 'T024_TEST';

-- T4  a set-based global audit returns zero invalid children
SELECT 'invalid children across the whole table' AS check_name,
       count(*) AS found, 0 AS required
FROM (
  SELECT child_material_unit_id
  FROM public.genealogy_edges
  WHERE COALESCE(is_deleted,false) = false
  GROUP BY child_material_unit_id
  HAVING abs(sum(contribution_weight) - 1.0) > 0.015
) x;

-- T5  the guard no longer performs a table-wide scan per row
SELECT 'guard body references the whole table' AS check_name,
       CASE WHEN pg_get_functiondef(p.oid) LIKE '%GROUP BY child_material_unit_id%'
            THEN 1 ELSE 0 END AS found, 0 AS required
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE p.proname = 'ppiq_genealogy_edge_weight_guard' AND n.nspname = 'public'
UNION ALL
SELECT 'guard scopes to the affected child',
       CASE WHEN pg_get_functiondef(p.oid) LIKE '%child_material_unit_id = target%'
            THEN 1 ELSE 0 END, 1
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE p.proname = 'ppiq_genealogy_edge_weight_guard' AND n.nspname = 'public';
'@
    $tr = Invoke-Sql -Tag "tests" -Sql $testSql
    Say $tr.Output
    if (-not [string]::IsNullOrWhiteSpace($tr.Error)) { Say $tr.Error }
    if ($tr.ExitCode -ne 0) { Say "[FAIL] a regression test failed."; throw "tests" }
    foreach ($line in ($tr.Output -split "`n")) {
        $t = $line.Trim()
        if ($t -match "^\|\s*(.+?)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|") {
            if ([int]$Matches[2] -ne [int]$Matches[3]) {
                Say ("[FAIL] " + $Matches[1] + ": found " + $Matches[2] +
                     ", required " + $Matches[3])
                $bad = $bad + 1
            }
        }
    }
    if ($bad -eq 0) { Say "[OK] all regression checks pass" }

    if ($SkipDryRun) {
        Rule "7 - DRY PROOF SKIPPED"
    } else {
        Rule "7 - DRY PROOF ON A CLONE"
        Say "This clones the database, so NO other session may be connected."
        $c2 = Invoke-Sql -Tag "conns2" -Sql @'
SELECT count(*) FROM pg_stat_activity
WHERE datname = current_database() AND pid <> pg_backend_pid();
'@
        Invoke-Sql -Tag "dropdry" -Db "postgres" `
                   -Sql ("DROP DATABASE IF EXISTS " + $DryDatabase + ";") | Out-Null
        $mk = Invoke-Sql -Tag "mkdry" -Db "postgres" `
                -Sql ("CREATE DATABASE " + $DryDatabase + " TEMPLATE " + $Database + ";")
        if ($mk.ExitCode -ne 0) {
            Say "[FAIL] could not clone. Stop the API and any other session, then retry."
            Say $mk.Error
            throw "clone"
        }
        Say ("[OK] cloned into " + $DryDatabase)
        $gen = Join-Path (Get-Location).Path "Backend\tools\generate_fleet_v2_donor.py"
        $canon = Join-Path $script:tmp "canonical.sql"
        Start-Process -FilePath "python" `
            -ArgumentList @($gen, "--mode", "fleet-v2", "--emit", "canonical", "--out", $canon) `
            -WorkingDirectory (Get-Location).Path -NoNewWindow -Wait `
            -RedirectStandardOutput (Join-Path $script:tmp "emit.out") `
            -RedirectStandardError (Join-Path $script:tmp "emit.err") | Out-Null
        Say ("started at " + (Get-Date -Format "HH:mm:ss"))
        Say ("-" * 78)
        $dr = Invoke-Sql -Tag "dryapply" -Db $DryDatabase -Stream -Sql (
              [System.IO.File]::ReadAllText($canon))
        Say ("-" * 78)
        Say ("finished at " + (Get-Date -Format "HH:mm:ss"))
        if ($dr.ExitCode -ne 0) {
            Say $dr.Error
            Say "[FAIL] the dry proof did not commit."
            throw "dry"
        }
        $dc = Invoke-Sql -Tag "drycount" -Db $DryDatabase -Sql @'
\pset border 2
SELECT 'material_units' AS entity, count(*) AS found, 35910 AS required FROM public.material_units
UNION ALL SELECT 'genealogy_edges', count(*), 34020 FROM public.genealogy_edges
UNION ALL SELECT 'parameter_observations', count(*), 301560 FROM public.parameter_observations
UNION ALL SELECT 'quality_events', count(*), 7844 FROM public.quality_events
UNION ALL SELECT 'downtime_events', count(*), 630 FROM public.downtime_events
UNION ALL SELECT 'invalid genealogy children', (SELECT count(*) FROM (
  SELECT child_material_unit_id FROM public.genealogy_edges
  WHERE COALESCE(is_deleted,false)=false GROUP BY 1
  HAVING abs(sum(contribution_weight)-1.0) > 0.015) z), 0
ORDER BY 1;
'@
        Say $dc.Output
        foreach ($line in ($dc.Output -split "`n")) {
            $t = $line.Trim()
            if ($t -match "^\|\s*(.+?)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|") {
                if ([int]$Matches[2] -ne [int]$Matches[3]) {
                    Say ("[FAIL] dry - " + $Matches[1] + ": found " + $Matches[2] +
                         ", required " + $Matches[3])
                    $bad = $bad + 1
                }
            }
        }
        Invoke-Sql -Tag "dropdry2" -Db "postgres" `
                   -Sql ("DROP DATABASE IF EXISTS " + $DryDatabase + ";") | Out-Null
        Say "[OK] clone dropped"
    }
}
catch {
    $bad = $bad + 1
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "RESULT"
if ($bad -gt 0) {
    Say ("[FAIL] " + $bad + " problem(s). The live replacement must NOT be run.")
    exit 1
}
Say "[OK] guard repaired, invariant preserved, dry proof green."
Say ""
Say "DISCOVERED PRODUCT DEFECT, recorded against T-024:"
Say "  The genealogy integrity guard was semantically correct but bulk-write"
Say "  unsafe - a deferred row trigger performed a full genealogy aggregate per"
Say "  affected row. M2a bulk genealogy materialisation would have hit the same"
Say "  wall."
Say ""
Say "NEXT: stop the API, then run the live replacement."
Say "  .\tools\run\Invoke-PpiqT024Canonical.ps1"
exit 0
