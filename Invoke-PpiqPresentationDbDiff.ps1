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
# RUN FROM REPO ROOT. Commands at the bottom.
# ============================================================================
[CmdletBinding()]
param(
    [ValidateSet("Diff", "ReVerify")]
    [string]$Mode = "Diff",

    [string]$LiveDb    = "ppiq_presentation",
    [string]$ScratchDb = "ppiq_presentation_scratch",
    [string]$DbHost    = "127.0.0.1",
    [int]   $Port      = 5432,
    [string]$User      = "ppiq_dev",
    [string]$Password  = "ppiq_dev_local_only",
    [switch]$SkipRebuild
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

$RepoRoot    = (Get-Location).Path
$EvidenceDir = Join-Path $RepoRoot "docs\m1\evidence"
$Stamp       = Get-Date -Format "yyyyMMdd_HHmmss"
$Rebuild     = Join-Path $RepoRoot "scripts\demo\Rebuild-PresentationDb.ps1"

$env:PGPASSWORD = $Password
$env:PGCLIENTENCODING = "UTF8"

$Lines = New-Object System.Collections.ArrayList
function Say([string]$T) { Write-Host $T; [void]$Lines.Add($T) }
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
    Head "1. REBUILD INTO SCRATCH"
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
foreach ($t in $all) {
    $l = 0; if ($lr.ContainsKey($t)) { $l = $lr[$t] }
    $s = 0; if ($sr.ContainsKey($t)) { $s = $sr[$t] }
    if ($l -ne $s) {
        $diffRows++
        Say ($t.PadRight(52) + $l.ToString().PadLeft(12) + $s.ToString().PadLeft(12) + "  " + ($s - $l))
    }
}
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
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqPresentationDbDiff.ps1 -Mode Diff
#
#   # read docs/m1/evidence/presentation_db_diff.txt and classify every line
#
#   # T-007. After every finding is a migration in Backend/database/scripts or a
#   # seed step in scripts/demo:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqPresentationDbDiff.ps1 -Mode ReVerify
#
#   # re-diff without paying for another rebuild, while iterating
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqPresentationDbDiff.ps1 -Mode Diff -SkipRebuild
#
#   git add -A
#   git commit -m "T-006: presentation database reproducibility diff"
# ============================================================================
