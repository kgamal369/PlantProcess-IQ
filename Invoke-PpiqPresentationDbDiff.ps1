# ============================================================================
# Invoke-PpiqPresentationDbDiff.ps1     Backlog v2.2 tasks T-006 and T-007
#
# THE QUESTION THIS ANSWERS
#   scripts/demo/Rebuild-PresentationDb.ps1 rebuilds the demo database in one
#   command, and its own header says the demo database is a reproducible
#   artifact, never truth. But it restores from
#   deploy/.ppiq-snapshots/ppiq_app_20260713_203359.dump - a 13 JULY snapshot.
#   Every correction made against the LIVE presentation database between 14 and
#   27 July survives a rebuild only if it also became one of the script's steps.
#
#   So: does the rebuild still reproduce the database you are about to
#   demonstrate? Nobody knows. This script finds out.
#
# -Mode Diff      T-006. Rebuild into a scratch database, then diff scratch
#                 against live: object inventory and row count per table.
#                 Writes docs/m1/evidence/presentation_db_diff.txt.
#
# -Mode ReVerify  T-007. Same rebuild and diff after you have converted every
#                 finding into a migration or a seed step, and it GATES: a
#                 non-empty diff exits non-zero. That output is the proof that
#                 no fix exists only as data.
#
# WHY IT NEVER TOUCHES THE LIVE DATABASE
#   Rebuild-PresentationDb.ps1 already guards on the target name containing
#   'presentation', which unhelpfully means ppiq_presentation itself passes.
#   This script adds a second guard: the scratch name must ALSO contain
#   'scratch', and it refuses if scratch and live resolve to the same database.
#
# WHAT THE FIRST RUN PROVED - TWO DEFECTS, ONE OF THEM SERIOUS
#
#   DEFECT A, mine. Rebuild-PresentationDb.ps1 does NOT create its target
#   database. It pg_restores into one that already exists. This script called
#   it against a scratch database that had never been created, so every step
#   failed with 'database ppiq_presentation_scratch does not exist'. Fixed:
#   this script now creates the scratch database first, and drops it first with
#   -Fresh so a half-built scratch cannot be mistaken for a rebuild.
#
#   DEFECT B, and this one matters far more than the diff.
#   REBUILD-PRESENTATIONDB.PS1 EXITED 0 AFTER EVERY SINGLE STEP FAILED.
#   pg_restore failed. Engine migrations 741 and 742 failed. All four Rule-1
#   fixes failed. Provenance neutralization failed. The engine-message scrub
#   failed. The dashboard upsert failed. The widget step was skipped. And the
#   script printed 'REBUILD COMPLETE' and returned success.
#
#   That script's own header calls itself 'the ONLY supported way to rebuild
#   the demo database'. If it is run on demonstration morning and anything
#   transient goes wrong, it will report success over a broken database and
#   nobody will know until a page is opened in the room. This is a Severity 1
#   defect in the demonstration toolchain and it needs its own task.
#
#   This script no longer trusts that exit code. After the rebuild it VERIFIES
#   the scratch database is readable and populated, and refuses to diff if not.
#   A diff against a database that was never built is a false green.
#
# RUN FROM REPO ROOT. Commands at the bottom.
# ============================================================================
[CmdletBinding()]
param(
    [ValidateSet("Diff", "ReVerify", "Detail")]
    [string]$Mode = "Diff",

    [string]$LiveDb    = "ppiq_presentation",
    [string]$ScratchDb = "ppiq_presentation_scratch",
    [string]$DbHost    = "127.0.0.1",
    [int]   $Port      = 5432,
    [string]$User      = "ppiq_dev",
    [string]$Password  = "ppiq_dev_local_only",
    [switch]$SkipRebuild,
    [switch]$Fresh
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

$RepoRoot    = (Get-Location).Path
$EvidenceDir = Join-Path $RepoRoot "docs\m1\evidence"
$Stamp       = Get-Date -Format "yyyyMMdd_HHmmss"
$Rebuild     = Join-Path $RepoRoot "scripts\demo\Rebuild-PresentationDb.ps1"

$env:PGPASSWORD = $Password
$env:PGCLIENTENCODING = "UTF8"

$IgnorePath  = Join-Path $RepoRoot "docs\m1\presentation_diff_ignore.txt"

$Lines = New-Object System.Collections.ArrayList
function Say([string]$T) { Write-Host $T; [void]$Lines.Add($T) }

# ---------------------------------------------------------------------------
# THE IGNORE LIST, AND WHY IT EXISTS.
# ReVerify demands an EMPTY diff. Ten tables in this database are records of
# things having happened - audit trails, refresh histories, job runs, refresh
# tokens - and they can never match between a fortnight-old live database and a
# fresh rebuild. Without this list T-006 was unachievable by construction, which
# was a defect in this script, not in the data.
#
# A table is compared BY DEFAULT. Ignoring one is a reviewed decision recorded
# in a checked-in file with a reason, exactly like the Rule 2 prefill allowlist,
# so a new table nobody classified shows up as a difference rather than
# vanishing. Seeding a fake audit trail would be worse than the mismatch.
# ---------------------------------------------------------------------------
function Get-IgnoreList {
    if (-not (Test-Path $IgnorePath)) {
        New-Item -ItemType Directory -Path (Split-Path $IgnorePath -Parent) -Force | Out-Null
        $seed = @'
# PRESENTATION DIFF - ROW-COUNT IGNORE LIST
#
# One qualified table per line, then a reason. These are records of things
# having happened. They cannot match between a live database and a fresh
# rebuild, and seeding a fake history would be worse than the mismatch.
#
# A table not listed here IS compared. Adding one is a reviewed decision.
# Schema objects are NEVER ignored - an object that exists only in live is a
# fix that is not in source control, and that is the point of this tool.
#
public.audit_log_entries            Runtime audit trail.
public.auth_refresh_tokens          Session tokens issued while the demo ran.
public.job_log                      Job execution log.
public.job_run_histories            Job run history.
public.read_model_refresh_runs      Read-model refresh history.
public.ml_correlation_compute_runs  Engine compute history.
public.ml_feature_store_refresh_runs Feature refresh history. NOTE: 11 in live vs 1 in scratch also signals the missing refresh step - do not let this line hide that.
public.ppiq_catalog_audit           Audit trail of the catalog.
public.ppiq_purge_audit             Audit trail of purges.
#
# NOT YET LISTED, DELIBERATELY:
# ppiq_forensics.wipe_audit - 18 trapped events. Read them before ignoring them.
# public.ppiq_layout_backup - decide whether the table is a feature or debris.
'@
        $enc = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($IgnorePath, ($seed -replace "`r`n", "`n" -replace "`n", "`r`n"), $enc)
    }
    $set = @{}
    foreach ($line in ([System.IO.File]::ReadAllText($IgnorePath) -split "`r?`n")) {
        $t = $line.Trim()
        if ($t -eq "" -or $t.StartsWith("#")) { continue }
        $set[($t -split "\s+")[0]] = $true
    }
    return $set
}
function Head([string]$T) { Say ""; Say ("=" * 78); Say $T; Say ("=" * 78) }

function Write-Utf8NoBom([string]$P, [string]$T) {
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($P, $T, $enc)
}

function Save([string]$Verdict) {
    New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null
    $name = "presentation_db_diff.txt"
    if ($Mode -eq "ReVerify") { $name = "presentation_db_diff_reverify_" + $Stamp + ".txt" }
    $out = Join-Path $EvidenceDir $name
    $head = @()
    $head += "T-006 / T-007 presentation database reproducibility diff"
    $head += ("Mode      : " + $Mode)
    $head += ("Timestamp : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
    $head += ("Live      : " + $LiveDb)
    $head += ("Scratch   : " + $ScratchDb)
    $head += ("Verdict   : " + $Verdict)
    $head += ""
    $body = (($head + $Lines.ToArray()) -join "`r`n")
    # every artifact this repository commits is pure ASCII
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $body.ToCharArray()) { if ([int]$ch -le 126 -and [int]$ch -ge 9) { [void]$sb.Append($ch) } }
    Write-Utf8NoBom $out $sb.ToString()
    Write-Host ""
    Write-Host ("[EVIDENCE] " + $out)
}

function Query([string]$Db, [string]$Sql) {
    $r = & psql -h $DbHost -p $Port -U $User -d $Db -tA -F "|" -v ON_ERROR_STOP=1 -c $Sql 2>&1
    if ($LASTEXITCODE -ne 0) { return $null }
    return @($r | Where-Object { $_ -ne "" })
}

$OBJECT_SQL = @"
SELECT kind || '|' || nspname || '.' || objname FROM (
  SELECT 'table' AS kind, n.nspname, c.relname AS objname
    FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE c.relkind='r' AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
  UNION ALL
  SELECT 'view', n.nspname, c.relname
    FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE c.relkind IN ('v','m') AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
  UNION ALL
  SELECT 'index', n.nspname, c.relname
    FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE c.relkind='i' AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
  UNION ALL
  SELECT 'function', n.nspname, p.proname
    FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
   WHERE n.nspname NOT IN ('pg_catalog','information_schema')
  UNION ALL
  SELECT 'trigger', n.nspname, t.tgname
    FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE NOT t.tgisinternal
) s ORDER BY 1;
"@

$ROWCOUNT_SQL = @"
SELECT n.nspname || '.' || c.relname || '|' ||
       (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', n.nspname, c.relname), false, true, '')))[1]::text
  FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE c.relkind='r' AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
 ORDER BY 1;
"@

Head ("PRESENTATION DATABASE REPRODUCIBILITY - MODE " + $Mode)

if ($Mode -eq "Detail") {
    # Enumerates the rows that exist ONLY in live, so they can be classified.
    # Read-only against both databases. Nothing is written to either.
    Head "DETAIL - WHAT EXISTS ONLY IN THE LIVE DATABASE"

    function ShowBoth([string]$label, [string]$sql) {
        Say ""
        Say ("--- " + $label + " ---")
        $liveRows = Query $LiveDb $sql
        $scrRows  = Query $ScratchDb $sql
        $scrSet = @{}
        if ($null -ne $scrRows) { foreach ($r in $scrRows) { $scrSet[$r] = $true } }
        if ($null -eq $liveRows) { Say "   (query failed against live)"; return }
        $only = @($liveRows | Where-Object { -not $scrSet.ContainsKey($_) })
        Say ("live " + $liveRows.Count + " / scratch " + $(if ($null -eq $scrRows) { 0 } else { $scrRows.Count }) + " / ONLY IN LIVE " + $only.Count)
        foreach ($r in $only) { Say ("   + " + $r) }
    }

    # v3.1: DISCOVER the columns instead of guessing them. Two queries failed on
    # the first run because I guessed `title` and `code`; the real names are
    # widget_title and dataset_code. The rebuild script learned this same lesson
    # in its own v2 - "parameter_definitions has no code column, v2 DISCOVERS the
    # real code column from information_schema" - and I repeated the mistake.
    function PickCol([string]$table, [string[]]$candidates, [string]$fallback) {
        foreach ($c in $candidates) {
            $r = Query $LiveDb ("SELECT 1 FROM information_schema.columns WHERE table_name='" + $table + "' AND column_name='" + $c + "' LIMIT 1;")
            if ($null -ne $r -and $r.Count -gt 0) { return $c }
        }
        Say ("   [WARN] none of (" + ($candidates -join ", ") + ") exist on " + $table + " - using " + $fallback)
        return $fallback
    }

    $wCode  = PickCol "dashboard_widget_definitions" @("widget_code", "code") "id::text"
    $wTitle = PickCol "dashboard_widget_definitions" @("widget_title", "title", "name") "id::text"
    $wChart = PickCol "dashboard_widget_definitions" @("chart_type", "widget_type", "kind") "id::text"
    $dCode  = PickCol "source_dataset_definitions" @("dataset_code", "code", "dataset_name") "id::text"
    Say ("[COLUMNS] widget code=" + $wCode + " title=" + $wTitle + " chart=" + $wChart + " | dataset=" + $dCode)

    ShowBoth "dashboards" "SELECT dashboard_code || ' | ' || COALESCE(name,'') || ' | system=' || is_system_template FROM dashboard_definitions WHERE COALESCE(is_deleted,false)=false ORDER BY 1;"
    ShowBoth "widgets"    ("SELECT COALESCE(w." + $wCode + ",'?') || ' | ' || COALESCE(w." + $wTitle + ",'') || ' | ' || COALESCE(w." + $wChart + ",'') || ' | dash=' || COALESCE(d.dashboard_code,'?') FROM dashboard_widget_definitions w LEFT JOIN dashboard_definitions d ON d.id=w.dashboard_definition_id WHERE COALESCE(w.is_deleted,false)=false ORDER BY 1;")
    ShowBoth "mapping versions" "SELECT id::text || ' | ' || COALESCE(status,'') FROM ppiq_mapping_versions ORDER BY 1;"
    ShowBoth "source dataset definitions" ("SELECT COALESCE(" + $dCode + ", id::text) FROM source_dataset_definitions ORDER BY 1;")

    Head "PPIQ-404 DEMO TRUTH TABLES - DO THEY EXIST, AND DOES ANY PRODUCT CODE READ THEM?"
    Say "The 18 trapped events are all one script dropping ppiq_p4_demo_features,"
    Say "ppiq_p4_demo_outcomes and ppiq_p4_demo_truth. A table named demo_truth,"
    Say "described as a demo correctness signal, is worth knowing the status of."
    $dt = Query $LiveDb "SELECT c.relname || ' | rows=' || COALESCE((xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', n.nspname, c.relname), false, true, '')))[1]::text,'?') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind='r' AND c.relname LIKE 'ppiq_p4_demo%' ORDER BY 1;"
    if ($null -eq $dt -or $dt.Count -eq 0) {
        Say "   NONE PRESENT in live. The script dropped them and nothing recreated them."
        Say "   If any product code path reads them, it is reading a table that no longer exists."
    } else {
        foreach ($r in $dt) { Say ("   " + $r) }
    }

    Head "THE 18 TRAPPED WIPE EVENTS"
    Say "These are the reason the forensics subsystem exists. Read them before"
    Say "deciding anything about it."
    $w = Query $LiveDb "SELECT * FROM ppiq_forensics.wipe_audit ORDER BY 1 LIMIT 30;"
    if ($null -eq $w) { Say "   (could not read ppiq_forensics.wipe_audit)" } else {
        foreach ($r in $w) { Say ("   " + $r) }
    }

    Save "DETAIL"
    exit 0
}

# ------------------------------------------------------------- GUARDS -------
if ($ScratchDb -notmatch "presentation") {
    Say "[REFUSED] the scratch name must contain 'presentation' - the rebuild script's own guard requires it."
    Save "REFUSED - SCRATCH NAME"; exit 1
}
if ($ScratchDb -notmatch "scratch") {
    Say "[REFUSED] the scratch name must ALSO contain 'scratch'."
    Say "          Rebuild-PresentationDb.ps1 only checks for 'presentation', which means"
    Say "          ppiq_presentation itself passes its guard. This second guard is what"
    Say "          stops this script rebuilding the database you are about to demonstrate."
    Save "REFUSED - SCRATCH NAME"; exit 1
}
if ($ScratchDb -eq $LiveDb) {
    Say "[REFUSED] scratch and live are the same database."
    Save "REFUSED - SAME DATABASE"; exit 1
}
if (-not (Test-Path $Rebuild)) {
    Say ("[REFUSED] not found: " + $Rebuild)
    Save "REFUSED - NO REBUILD SCRIPT"; exit 1
}
Say ("[OK] guards passed. Live " + $LiveDb + " is never written by this script.")

# ------------------------------------------------------------- REBUILD ------
if (-not $SkipRebuild) {
    Head "1a. CREATE THE SCRATCH DATABASE"
    Say "Rebuild-PresentationDb.ps1 restores INTO an existing database; it does not"
    Say "create one. The first run of this script failed for exactly that reason."

    if ($Fresh) {
        & psql -h $DbHost -p $Port -U $User -d postgres -v ON_ERROR_STOP=1 -c ("DROP DATABASE IF EXISTS " + $ScratchDb) 2>&1 | Out-Null
        Say ("[DROP] " + $ScratchDb + " - a half-built scratch must never be mistaken for a rebuild")
    }

    $exists = ("" + (& psql -h $DbHost -p $Port -U $User -d postgres -tAc ("SELECT 1 FROM pg_database WHERE datname='" + $ScratchDb + "'"))).Trim()
    if ($exists -eq "1") {
        Say ("[SKIP] " + $ScratchDb + " already exists. Use -Fresh to rebuild it from nothing.")
    } else {
        & psql -h $DbHost -p $Port -U $User -d postgres -v ON_ERROR_STOP=1 -c ("CREATE DATABASE " + $ScratchDb + " OWNER " + $User) 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Say ("[FAIL] could not create " + $ScratchDb)
            Save "SCRATCH CREATE FAILED"; exit 1
        }
        Say ("[OK] created " + $ScratchDb)
    }

    Head "1b. REBUILD INTO SCRATCH"
    Say "Running scripts/demo/Rebuild-PresentationDb.ps1 with -TargetDb pointed at scratch."
    Say "This restores the 13 July snapshot and replays every scripted step on top."
    Say ""

    $prevConsole = [Console]::OutputEncoding
    $prevOut = $OutputEncoding
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try {
        [Console]::OutputEncoding = $utf8
        $OutputEncoding = $utf8
        & powershell -NoProfile -ExecutionPolicy Bypass -File $Rebuild -TargetDb $ScratchDb -Execute 2>&1 |
            ForEach-Object {
                $clean = [regex]::Replace([string]$_, "\x1B\[[0-9;?]*[A-Za-z]", "")
                Say $clean
            }
        $rc = $LASTEXITCODE
    }
    finally {
        [Console]::OutputEncoding = $prevConsole
        $OutputEncoding = $prevOut
    }
    Say ("[REBUILD] exit code " + $rc)
    if ($rc -ne 0) {
        Say "[STOP] the rebuild did not complete. Nothing can be concluded from a diff against a half-built database."
        Save "REBUILD FAILED"; exit 1
    }

    # The exit code is not evidence. On 02-Aug this script reported exit 0 while
    # every step inside the rebuild had failed. Read the output instead.
    $joined = ($Lines.ToArray() -join "`n")
    $failLines = @([regex]::Matches($joined, '(?m)^\s*(FAIL|FAILED)\b.*$') | ForEach-Object { $_.Value.Trim() })
    if ($failLines.Count -gt 0) {
        Say ""
        Say ("[STOP] the rebuild returned exit 0 but printed " + $failLines.Count + " failure lines:")
        foreach ($f in $failLines) { Say ("   " + $f) }
        Say ""
        Say "A script that reports success while failing is worse than one that fails,"
        Say "because it teaches everyone to trust the wrong signal. Fix the rebuild's"
        Say "exit code before using it again - it calls itself the only supported way"
        Say "to rebuild the demonstration database."
        Save "REBUILD SILENTLY FAILED"; exit 1
    }

    Head "1c. VERIFY THE SCRATCH DATABASE IS ACTUALLY BUILT"
    $tblCount = ("" + (& psql -h $DbHost -p $Port -U $User -d $ScratchDb -tAc "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind='r' AND n.nspname='public'")).Trim()
    if ($LASTEXITCODE -ne 0 -or $tblCount -eq "" -or [int]$tblCount -lt 20) {
        Say ("[STOP] scratch holds " + $tblCount + " public tables. It was not built.")
        Say "       A diff against a database that does not exist is a false green."
        Save "SCRATCH NOT BUILT"; exit 1
    }
    $mu = ("" + (& psql -h $DbHost -p $Port -U $User -d $ScratchDb -tAc "SELECT count(*) FROM material_units")).Trim()
    Say ("[OK] scratch has " + $tblCount + " public tables and " + $mu + " material_units")
    if ($mu -ne "" -and [int]$mu -eq 0) {
        Say "[STOP] zero material units. The restore did not load data."
        Save "SCRATCH EMPTY"; exit 1
    }
} else {
    Head "1. REBUILD SKIPPED"
    Say "-SkipRebuild given. The scratch database is used as it stands."
}

# ---------------------------------------------------------- OBJECT DIFF -----
Head "2. OBJECT INVENTORY"

$liveObj = Query $LiveDb $OBJECT_SQL
$scrObj  = Query $ScratchDb $OBJECT_SQL

if ($null -eq $liveObj) { Say ("[FAIL] could not read " + $LiveDb); Save "LIVE UNREADABLE"; exit 1 }
if ($null -eq $scrObj)  { Say ("[FAIL] could not read " + $ScratchDb); Save "SCRATCH UNREADABLE"; exit 1 }

Say ("live objects    : " + $liveObj.Count)
Say ("scratch objects : " + $scrObj.Count)

$liveSet = @{}
foreach ($o in $liveObj) { $liveSet[$o] = $true }
$scrSet = @{}
foreach ($o in $scrObj) { $scrSet[$o] = $true }

$onlyLive = @($liveObj | Where-Object { -not $scrSet.ContainsKey($_) })
$onlyScr  = @($scrObj  | Where-Object { -not $liveSet.ContainsKey($_) })

Say ""
Say ("ONLY IN LIVE    : " + $onlyLive.Count + "   <- these exist only as data. Each one is a fix that is not in source control.")
foreach ($o in $onlyLive) { Say ("   + " + $o) }
Say ""
Say ("ONLY IN SCRATCH : " + $onlyScr.Count + "   <- these are in source control but absent from live. The live database is behind.")
foreach ($o in $onlyScr) { Say ("   - " + $o) }

# --------------------------------------------------------- ROW COUNT DIFF ---
Head "3. ROW COUNTS"

$liveRows = Query $LiveDb $ROWCOUNT_SQL
$scrRows  = Query $ScratchDb $ROWCOUNT_SQL

$lr = @{}
foreach ($r in $liveRows) { $p = $r -split "\|"; if ($p.Count -ge 2) { $lr[$p[0]] = [int64]$p[1] } }
$sr = @{}
foreach ($r in $scrRows)  { $p = $r -split "\|"; if ($p.Count -ge 2) { $sr[$p[0]] = [int64]$p[1] } }

$all = @($lr.Keys) + @($sr.Keys) | Sort-Object -Unique
$diffRows = 0
Say ("table".PadRight(52) + "live".PadLeft(12) + "scratch".PadLeft(12) + "  delta")
Say ("-" * 92)
$Ignored = Get-IgnoreList
$ignoredHits = 0
foreach ($t in $all) {
    $l = 0; if ($lr.ContainsKey($t)) { $l = $lr[$t] }
    $s = 0; if ($sr.ContainsKey($t)) { $s = $sr[$t] }
    if ($l -ne $s) {
        if ($Ignored.ContainsKey($t)) {
            $ignoredHits++
            Say ($t.PadRight(52) + $l.ToString().PadLeft(12) + $s.ToString().PadLeft(12) + "  " + ($s - $l) + "   (ignored: runtime history)")
            continue
        }
        $diffRows++
        Say ($t.PadRight(52) + $l.ToString().PadLeft(12) + $s.ToString().PadLeft(12) + "  " + ($s - $l))
    }
}
Say ""
Say ("Ignored by " + $IgnorePath + " : " + $ignoredHits + " table(s)")
Say "Schema objects are never ignored."
if ($diffRows -eq 0) { Say "(no table differs)" }

# ------------------------------------------------------------- VERDICT -----
Head "4. VERDICT"

$total = $onlyLive.Count + $onlyScr.Count + $diffRows
Say ("objects only in live    : " + $onlyLive.Count)
Say ("objects only in scratch : " + $onlyScr.Count)
Say ("tables with a row delta : " + $diffRows)
Say ("TOTAL DIFFERENCES       : " + $total)
Say ""

if ($total -eq 0) {
    Say "[EMPTY DIFF] the rebuild reproduces the live presentation database exactly."
    Say "             No fix exists only as data. This is the T-007 acceptance output."
    Save "EMPTY DIFF"
    exit 0
}

if ($Mode -eq "Diff") {
    Say "[T-006 COMPLETE] the list above IS the deliverable. Do not proceed to any other"
    Say "                 task until every line has been classified, and never run the"
    Say "                 rebuild against the live database before that is done."
    Say ""
    Say "CLASSIFY EACH LINE. The governing law: presentation DATA may be"
    Say "presentation-only; presentation FIXES may never be."
    Say ""
    Say "  A schema object, a function, a trigger, an index, a view"
    Say "     -> PRODUCT FIX. Backend/database/scripts, as a new numbered migration."
    Say ""
    Say "  A row count difference in a plant, canonical or intelligence table"
    Say "     -> PRESENTATION DATA. scripts/demo, as a seed step in the rebuild."
    Say ""
    Say "  A row count difference in a definition, registry or configuration table"
    Say "     -> READ IT TWICE. A seeded dashboard is presentation data. A corrected"
    Say "        widget definition, a repointed outcome key or a fixed mapping is a"
    Say "        PRODUCT FIX wearing data's clothes, and it belongs in a migration."
    Say ""
    Say "  A row present in live and absent in scratch, in a table nobody seeded"
    Say "     -> somebody typed into the demo database by hand. That is the case this"
    Say "        whole task exists to find."
    Say ""
    Say "Then run -Mode ReVerify. It gates: a non-empty diff exits non-zero."
    Save "DIFF - CLASSIFICATION REQUIRED"
    exit 0
}

Say "[T-007 NOT MET] the rebuild still does not reproduce the live database."
Say "                Every line above is a fix or a datum that exists only in the"
Say "                live database and nowhere in source control."
Save "REVERIFY FAILED"
exit 1

# ============================================================================
# HOW TO RUN
#
#   cd C:\Workspace\PlantProcess-IQ
#
#   # T-006. Stop the API first - the rebuild stops it anyway, but a clean start
#   # makes the log readable.
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run\free-ports.ps1 -Ports 5063 -Force
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqPresentationDbDiff.ps1 -Mode Diff -Fresh
#
#   # enumerate the rows that exist only in live, so they can be classified
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqPresentationDbDiff.ps1 -Mode Detail
#
#   # read docs/m1/evidence/presentation_db_diff.txt and classify every line
#
#   # T-007. After every finding is a migration in Backend/database/scripts or a
#   # seed step in scripts/demo:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqPresentationDbDiff.ps1 -Mode ReVerify
#
#   # re-diff without paying for another rebuild, while iterating
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqPresentationDbDiff.ps1 -Mode Diff -Fresh -SkipRebuild
#
#   git add -A
#   git commit -m "T-006: presentation database reproducibility diff"
# ============================================================================
