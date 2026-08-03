# ============================================================================
# Cleanup-PpiqLocalDatabases.ps1
#
# Acts on the infrastructure ruling. Target clean state is exactly three:
#     postgres, ppiq_app, ppiq_presentation
#
# WHAT IT DOES
#   -Report            measure only. Size, active connections, and for
#                      plantprocessiq the object and row profile that decides
#                      whether it is legacy. NOTHING is dropped.
#   -DropDisposable    drops ppiq_presentation_scratch and ppiq_acceptance_empty.
#                      Both are disposable BY DESIGN: the scratch is recreated by
#                      any diff run with -Fresh, and T-004 moved to M2a-P3 as an
#                      ephemeral fixture, so no permanent acceptance database is
#                      needed.
#   -DropLegacy        drops plantprocessiq. REFUSES unless -Report has been run
#                      and the database has no active connections. A backup is
#                      taken first, always, whatever you say.
#
# WHAT IT WILL NEVER DO
#   postgres, ppiq_app and ppiq_presentation are on a hard deny list. The drop
#   function refuses them by name before it looks at anything else.
#
# A NOTE ON THE RAM, BECAUSE THIS SCRIPT WILL NOT FIX IT
#   Six databases in ONE PostgreSQL instance is not six engines. Dropping three
#   frees disk, not gigabytes of memory. VmmemWSL at ~3.77 GB is Docker, and the
#   weight there is caster-oracle, hsm-oracle and pkl-mssql. Stopping those three
#   will do more for memory than everything this script does. Stop, never
#   'down -v' - the volumes hold the fixtures.
#
# RUN FROM REPO ROOT. Commands at the bottom.
# ============================================================================
[CmdletBinding()]
param(
    [switch]$Report,
    [switch]$DropDisposable,
    [switch]$DropLegacy,
    [string]$DbHost   = "127.0.0.1",
    [int]   $Port     = 5432,
    [string]$User     = "ppiq_dev",
    [string]$Password = "ppiq_dev_local_only"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

$RepoRoot    = (Get-Location).Path
$EvidenceDir = Join-Path $RepoRoot "docs\m1\evidence"
$BackupDir   = Join-Path $RepoRoot "deploy\.ppiq-snapshots"
$Stamp       = Get-Date -Format "yyyyMMdd_HHmmss"

$NeverDrop = @("postgres", "ppiq_app", "ppiq_presentation", "template0", "template1")

$env:PGPASSWORD = $Password
$env:PGCLIENTENCODING = "UTF8"

function Head([string]$Banner) { Write-Host ""; Write-Host ("=" * 78); Write-Host $Banner; Write-Host ("=" * 78) }

# SQL to a file, run with -f, results via -o. Never -c for anything multi-line.
function Rows([string]$Database, [string]$Sql) {
    $gid = [guid]::NewGuid().ToString("N")
    $qF = Join-Path $env:TEMP ("ppiq_clean_q_" + $gid + ".sql")
    $rF = Join-Path $env:TEMP ("ppiq_clean_r_" + $gid + ".txt")
    $eF = Join-Path $env:TEMP ("ppiq_clean_e_" + $gid + ".txt")
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($qF, $Sql, $enc)
    & psql -h $DbHost -p $Port -U $User -d $Database -w -X -A -F "|" -t -v ON_ERROR_STOP=1 -o $rF -f $qF 2>$eF | Out-Null
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

function Drop-Database([string]$Name) {
    if ($NeverDrop -contains $Name) {
        Write-Host ("[REFUSED] " + $Name + " is on the hard deny list. Nothing done.")
        return $false
    }
    $exists = @(Rows "postgres" ("SELECT 1 FROM pg_database WHERE datname = '" + $Name + "';"))
    if ($exists.Count -eq 0) {
        Write-Host ("[SKIP] " + $Name + " does not exist.")
        return $true
    }
    $conns = @(Rows "postgres" ("SELECT count(*) FROM pg_stat_activity WHERE datname = '" + $Name + "';"))
    $n = 0
    if ($conns.Count -gt 0) { $n = [int]([string]$conns[0]).Trim() }
    if ($n -gt 0) {
        Write-Host ("[REFUSED] " + $Name + " has " + $n + " active connection(s). Close them first.")
        Write-Host "          A database in use is a database somebody is using."
        return $false
    }
    & psql -h $DbHost -p $Port -U $User -d postgres -w -X -v ON_ERROR_STOP=1 -c ("DROP DATABASE " + $Name) 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host ("[DROPPED] " + $Name)
        return $true
    }
    Write-Host ("[FAIL] could not drop " + $Name)
    return $false
}

if (-not ($Report -or $DropDisposable -or $DropLegacy)) {
    Write-Host "Pass one of -Report, -DropDisposable or -DropLegacy. Nothing done."
    exit 1
}

# ---------------------------------------------------------------- REPORT ----
Head "1. EVERY DATABASE ON THIS SERVER"
foreach ($r in (Rows "postgres" @"
SELECT d.datname,
       pg_size_pretty(pg_database_size(d.datname)),
       (SELECT count(*) FROM pg_stat_activity a WHERE a.datname = d.datname)
FROM pg_database d
WHERE NOT d.datistemplate
ORDER BY pg_database_size(d.datname) DESC;
"@)) { Write-Host ("   " + $r) }

Write-Host ""
Write-Host "   name | size | active connections"
Write-Host ""
Write-Host "   KEEP  postgres, ppiq_app, ppiq_presentation"
Write-Host "   DROP  ppiq_presentation_scratch  - recreated by any diff run with -Fresh"
Write-Host "   DROP  ppiq_acceptance_empty      - T-004 is an ephemeral M2a fixture now"
Write-Host "   VERIFY plantprocessiq            - likely legacy, profiled below"

Head "2. IS plantprocessiq LEGACY? THE PROFILE THAT DECIDES"
$lives = @(Rows "postgres" "SELECT 1 FROM pg_database WHERE datname = 'plantprocessiq';")
if ($lives.Count -eq 0) {
    Write-Host "   plantprocessiq does not exist on this server. Nothing to decide."
} else {
    Write-Host "   Tables with rows, largest first - if this is a working database it shows here:"
    foreach ($r in (Rows "plantprocessiq" @"
SELECT n.nspname || '.' || c.relname,
       (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', n.nspname, c.relname), false, true, '')))[1]::text
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r' AND n.nspname NOT IN ('pg_catalog', 'information_schema')
ORDER BY 2 DESC
LIMIT 20;
"@)) { Write-Host ("      " + $r) }

    Write-Host ""
    Write-Host "   Most recent activity anywhere in it:"
    foreach ($r in (Rows "plantprocessiq" @"
SELECT schemaname || '.' || relname, n_tup_ins, n_tup_upd, n_tup_del
FROM pg_stat_user_tables
WHERE n_tup_ins + n_tup_upd + n_tup_del > 0
ORDER BY n_tup_ins + n_tup_upd + n_tup_del DESC
LIMIT 10;
"@)) { Write-Host ("      " + $r) }

    Write-Host ""
    Write-Host "   READ THIS BEFORE DROPPING: if the table list is empty or holds only"
    Write-Host "   EF migration history, it is legacy. If it carries plant data with row"
    Write-Host "   counts you do not recognise, STOP and find out where they came from."
}

if ($Report) {
    Write-Host ""
    Write-Host "[REPORT ONLY] nothing was dropped."
    Write-Host "Next: -DropDisposable, then -DropLegacy once you have read section 2."
    exit 0
}

# ------------------------------------------------------------- DISPOSABLE ---
if ($DropDisposable) {
    Head "3. DROP THE DISPOSABLE DATABASES"
    [void](Drop-Database "ppiq_presentation_scratch")
    [void](Drop-Database "ppiq_acceptance_empty")
    Write-Host ""
    Write-Host "Neither is a loss. The scratch is rebuilt by:"
    Write-Host "  .\Invoke-PpiqPresentationDbDiff.ps1 -Mode Diff -Fresh"
}

# ----------------------------------------------------------------- LEGACY ---
if ($DropLegacy) {
    Head "4. plantprocessiq - BACKUP FIRST, ALWAYS"
    $lives2 = @(Rows "postgres" "SELECT 1 FROM pg_database WHERE datname = 'plantprocessiq';")
    if ($lives2.Count -eq 0) {
        Write-Host "[SKIP] plantprocessiq does not exist."
    } else {
        New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
        $dump = Join-Path $BackupDir ("plantprocessiq_legacy_" + $Stamp + ".dump")
        Write-Host ("Backing up to " + $dump)
        & pg_dump -h $DbHost -p $Port -U $User -d plantprocessiq -Fc -f $dump 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $dump)) {
            Write-Host "[REFUSED] the backup failed. A database is not dropped without one."
            exit 1
        }
        $mb = [Math]::Round((Get-Item $dump).Length / 1MB, 1)
        Write-Host ("[BACKUP] " + $mb + " MB")
        if ($mb -lt 0.01) {
            Write-Host "[REFUSED] the backup is suspiciously small. Check it before dropping."
            exit 1
        }
        [void](Drop-Database "plantprocessiq")
    }
}

Head "DONE"
Write-Host "Remaining databases:"
foreach ($r in (Rows "postgres" "SELECT datname FROM pg_database WHERE NOT datistemplate ORDER BY 1;")) {
    Write-Host ("   " + $r)
}
Write-Host ""
Write-Host "THE MEMORY IS NOT HERE. Six databases in one instance is not six engines."
Write-Host "For RAM, stop the source containers - caster-oracle, hsm-oracle and"
Write-Host "pkl-mssql are the heavy three:"
Write-Host "   docker compose -f deploy/compose/docker-compose.sources.yml stop"
Write-Host "STOP, never 'down -v'. The volumes hold the fixtures."

# ============================================================================
# HOW TO RUN
#
#   cd C:\Workspace\PlantProcess-IQ
#
#   # 1. measure. Read section 2 before going further.
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Cleanup-PpiqLocalDatabases.ps1 -Report
#
#   # 2. the two that are disposable by design
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Cleanup-PpiqLocalDatabases.ps1 -DropDisposable
#
#   # 3. only after reading the profile - backs up first, refuses without one
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Cleanup-PpiqLocalDatabases.ps1 -DropLegacy
#
#   # 4. the actual memory win
#   docker compose -f deploy/compose/docker-compose.sources.yml stop
# ============================================================================
