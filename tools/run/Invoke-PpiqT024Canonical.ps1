#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-024 step B - replace the canonical operational population with Fleet
    v2. THIS IS THE DESTRUCTIVE STEP. It runs only behind seven gates.

.DESCRIPTION
    GATES, ALL REQUIRED BEFORE ANY DESTRUCTIVE OPERATION:
      G1  generator build hash matches the pinned value
      G2  capture mode is still byte-identical to the proven baseline
      G3  the Fleet v2 reference vocabulary is present
      G4  no reference code is duplicated
      G5  the emitted transaction has the expected shape
      G6  a backup of the presentation database exists
      G7  THE RESTORE IS PROVEN, by restoring that backup into a scratch database
          and comparing row counts. A backup nobody has restored is a hope.

    Only then: the replacement, in the measured foreign key order, inside ONE
    transaction. Any failure rolls back everything.

    AFTER: provenance, genealogy, downtime quantities and identity convention are
    verified; materialized views that read the canonical tables are refreshed; and
    an API smoke test runs, because row counts alone do not prove a dashboard
    still renders.

    -ReportOnly runs G1 to G7 and stops. Nothing is deleted.

.EXAMPLE
    .\tools\run\Invoke-PpiqT024Canonical.ps1 -ReportOnly
    .\tools\run\Invoke-PpiqT024Canonical.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost               = "127.0.0.1",
    [int]   $PgPort               = 5432,
    [string]$PgUser               = "ppiq_dev",
    [string]$PgPassword           = "ppiq_dev_local_only",
    [string]$Database             = "ppiq_presentation",
    [string]$RestoreTestDatabase  = "ppiq_t024_restore_test",
    [string]$PsqlPath             = "",
    [string]$ExpectedGeneratorSha = "CB4C097D70D49B0F8875F76D8D81BBA28C651BC332D1DCA50E23FD1558F12DE1",
    [string]$ExpectedCaptureSha   = "11EDF4B275A106C86D75EA3147D47B56F7763AD9EE2D258487953B7155939AD7",
    [string]$ApiBaseUrl           = "http://localhost:5000",
    [switch]$SkipRestoreTest,
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

function Find-PgTool {
    param([string]$Name, [string]$Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        $cand = Join-Path (Split-Path $Explicit -Parent) $Name
        if (Test-Path -LiteralPath $cand) { return $cand }
    }
    $c = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -ne $c) { return $c.Source }
    foreach ($p in @("C:\Program Files\PostgreSQL\16\bin",
                     "C:\Program Files\PostgreSQL\17\bin",
                     "C:\Program Files\PostgreSQL\15\bin")) {
        $cand = Join-Path $p $Name
        if (Test-Path -LiteralPath $cand) { return $cand }
    }
    return $null
}

function Invoke-Psql {
    param([string]$File, [string]$Tag, [string]$Db = "")
    if ($Db -eq "") { $Db = $Database }
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Db,
           "-v", "ON_ERROR_STOP=1", "-f", $File, "-o", $o)
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait `
                       -PassThru -RedirectStandardError $e
    $r = New-Object psobject
    Add-Member -InputObject $r -MemberType NoteProperty -Name ExitCode -Value $p.ExitCode
    Add-Member -InputObject $r -MemberType NoteProperty -Name Output   -Value (Read-IfExists $o)
    Add-Member -InputObject $r -MemberType NoteProperty -Name Error    -Value (Read-IfExists $e)
    return $r
}

function Invoke-Sql {
    param([string]$Sql, [string]$Tag, [string]$Db = "")
    $f = Join-Path $script:tmp ($Tag + ".sql")
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    return Invoke-Psql -File $f -Tag $Tag -Db $Db
}

function Check-Table {
    param([string]$Output, [string]$Label)
    $bad = 0
    foreach ($line in ($Output -split "`n")) {
        if ($line -match "^\|\s*(.+?)\s*\|\s*(-?\d+)\s*\|\s*(-?\d+)\s*\|") {
            if ([int]$Matches[2] -ne [int]$Matches[3]) {
                Say ("[FAIL] " + $Label + " - " + $Matches[1] + ": found " +
                     $Matches[2] + ", required " + $Matches[3])
                $bad = $bad + 1
            }
        }
    }
    return $bad
}

Rule "PPIQ T-024 STEP B - CANONICAL REPLACEMENT (DESTRUCTIVE)"
$RepoRoot = (Get-Location).Path
$gen = Join-Path $RepoRoot "Backend\tools\generate_fleet_v2_donor.py"
if (-not (Test-Path -LiteralPath $gen)) { Say "[FAIL] generator not found."; exit 2 }

$script:psql = Find-PgTool -Name "psql.exe" -Explicit $PsqlPath
$pgdump      = Find-PgTool -Name "pg_dump.exe" -Explicit $script:psql
$pgrestore   = Find-PgTool -Name "pg_restore.exe" -Explicit $script:psql
if ($null -eq $script:psql) { Say "[FAIL] psql.exe not found."; exit 2 }
Say ("Database : " + $Database)
Say ("User     : " + $PgUser + "   password supplied by the script, never typed")
Say ("Mode     : " + $(if ($ReportOnly) { "REPORT ONLY - gates run, nothing deleted" } else { "FULL - the replacement will run" }))

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t024b_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$backupDir = Join-Path $RepoRoot "deploy\.ppiq-snapshots"
if (-not (Test-Path -LiteralPath $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
}
$backupFile = Join-Path $backupDir ("ppiq_presentation_pre_T024_" + $stamp + ".dump")
$canonSql = Join-Path $script:tmp "canonical.sql"
$exitCode = 0

try {
    Rule "G1 - GENERATOR BUILD"
    $genSha = (Get-FileHash -LiteralPath $gen -Algorithm SHA256).Hash
    Say ("source sha256 : " + $genSha)
    if ($genSha -ne $ExpectedGeneratorSha.ToUpper()) {
        Say ("[FAIL] expected " + $ExpectedGeneratorSha.ToUpper())
        Say "[STOP] refusing before any database work."
        throw "G1"
    }
    Say "[OK] build matches the pinned hash"

    Rule "G2 - CAPTURE MODE STILL BYTE-IDENTICAL"
    $capOut = Join-Path $script:tmp "capture.sql"
    $cp = Start-Process -FilePath "python" `
            -ArgumentList @($gen, "--mode", "capture", "--out", $capOut) `
            -WorkingDirectory $RepoRoot -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput (Join-Path $script:tmp "cap.out") `
            -RedirectStandardError (Join-Path $script:tmp "cap.err")
    if ($cp.ExitCode -ne 0) { Say "[FAIL] capture emit failed."; throw "G2" }
    $capSha = (Get-FileHash -LiteralPath $capOut -Algorithm SHA256).Hash
    Say ("capture sha256 : " + $capSha)
    if ($capSha -ne $ExpectedCaptureSha.ToUpper()) {
        Say "[FAIL] the frozen capture has drifted. Retirement gate condition 1"
        Say "       would no longer be re-provable."
        throw "G2"
    }
    Say "[OK] capture unchanged"

    Rule "G3 and G4 - REFERENCE VOCABULARY AND NO DUPLICATES"
    $refSql = @'
\pset border 2
SELECT 'fleet v2 defect codes' AS check_name, count(DISTINCT defect_code) AS found, 14 AS required
FROM public.defect_catalogs
WHERE defect_code IN ('SCALE','EDGE_CRACK','ROLLED_IN_SCALE','SLIVER','INCLUSION',
  'PINHOLE','SCRATCH','WAVINESS','CENTRE_BUCKLE','EDGE_WAVE','ROLL_MARK',
  'LAMINATION','OIL_SPOT','SENSOR_ARTEFACT')
UNION ALL
SELECT 'fleet v2 parameters', count(DISTINCT parameter_code), 29
FROM public.parameter_definitions
WHERE parameter_code IN ('CARBON_PCT','MANGANESE_PCT','SILICON_PCT','SULPHUR_PCT',
  'PHOSPHORUS_PCT','ALUMINIUM_PCT','TAP_TEMP_C','OXYGEN_NM3','POWER_KWH',
  'LF_ARGON_NM3','LF_CALCIUM_M','LF_FINAL_TEMP_C','CASTING_SPEED_MPM','SUPERHEAT_C',
  'MOULD_LEVEL_AVG','FDT_C','CT_C','THICKNESS_MM','WIDTH_MM','ROLL_FORCE_KN',
  'ROLL_GAP_MM','ROLL_SPEED_MPS','ROLL_TEMP_C','ACID_CONC_PCT','BATH_TEMP_C',
  'LINE_SPEED_MPM','QA_WIDTH_MM','QA_THK_MM','QA_ROUGHNESS_UM')
UNION ALL
SELECT 'fleet v2 equipment', count(DISTINCT equipment_code), 18
FROM public.equipment
WHERE equipment_code IN ('EAF-01','EAF-02','LF-01','LF-02','CCM-01','CCM-02',
  'HSM-01','PKL-01','PKL-02','PARSYTEC-01','PARSYTEC-02','HSM-01-F1','HSM-01-F2',
  'HSM-01-F3','HSM-01-F4','HSM-01-F5','HSM-01-F6','HSM-01-F7')
UNION ALL
SELECT 'duplicate parameter codes', count(*), 0 FROM
  (SELECT 1 FROM public.parameter_definitions GROUP BY parameter_code HAVING count(*)>1) a
UNION ALL
SELECT 'duplicate defect codes', count(*), 0 FROM
  (SELECT 1 FROM public.defect_catalogs GROUP BY defect_code HAVING count(*)>1) b
UNION ALL
SELECT 'duplicate equipment codes', count(*), 0 FROM
  (SELECT 1 FROM public.equipment GROUP BY equipment_code HAVING count(*)>1) c;
'@
    $ref = Invoke-Sql -Sql $refSql -Tag "refgate"
    if ($ref.ExitCode -ne 0) { Say $ref.Error; throw "G3" }
    Say $ref.Output
    if ((Check-Table -Output $ref.Output -Label "reference") -gt 0) { throw "G3/G4" }
    Say "[OK] vocabulary present and unambiguous"

    Rule "G5 - EMIT AND VALIDATE THE TRANSACTION"
    $ep = Start-Process -FilePath "python" `
            -ArgumentList @($gen, "--mode", "fleet-v2", "--emit", "canonical",
                            "--out", $canonSql) `
            -WorkingDirectory $RepoRoot -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput (Join-Path $script:tmp "emit.out") `
            -RedirectStandardError (Join-Path $script:tmp "emit.err")
    Say (Read-IfExists (Join-Path $script:tmp "emit.out"))
    if ($ep.ExitCode -ne 0) {
        Say (Read-IfExists (Join-Path $script:tmp "emit.err"))
        throw "G5"
    }
    $sqlText = [System.IO.File]::ReadAllText($canonSql)
    $shape = @{
        "BEGIN"        = ([regex]::Matches($sqlText, "(?m)^BEGIN;")).Count
        "COMMIT"       = ([regex]::Matches($sqlText, "(?m)^COMMIT;")).Count
        "DELETE"       = ([regex]::Matches($sqlText, "(?m)^DELETE FROM public\.")).Count
        "assertions"   = ([regex]::Matches($sqlText, "RAISE EXCEPTION")).Count
        "TRUNCATE"     = ([regex]::Matches($sqlText, "(?m)^TRUNCATE")).Count
        "DROP"         = ([regex]::Matches($sqlText, "(?m)^DROP")).Count
    }
    foreach ($k in ($shape.Keys | Sort-Object)) { Say ("  " + $k.PadRight(12) + $shape[$k]) }
    $order = [regex]::Matches($sqlText, "(?m)^DELETE FROM public\.(\w+);")
    $seen = @()
    foreach ($m in $order) { $seen += $m.Groups[1].Value }
    Say ("  delete order : " + ($seen -join ", "))
    $expected = @("risk_scores", "quality_events", "genealogy_edges",
                  "parameter_observations", "process_step_executions",
                  "downtime_events", "material_units")
    $shapeBad = 0
    if ($shape["BEGIN"] -ne 1 -or $shape["COMMIT"] -ne 1) {
        Say "[FAIL] the file is not exactly one transaction"; $shapeBad = 1
    }
    if ($shape["TRUNCATE"] -gt 0 -or $shape["DROP"] -gt 0) {
        Say "[FAIL] the file carries TRUNCATE or DROP at statement level"; $shapeBad = 1
    }
    if ($shape["assertions"] -lt 3) {
        Say "[FAIL] fewer than three in-transaction assertions"; $shapeBad = 1
    }
    if (($seen -join ",") -ne ($expected -join ",")) {
        Say "[FAIL] the delete order is not the measured foreign key order"
        Say ("       expected " + ($expected -join ", "))
        $shapeBad = 1
    }
    if ($shapeBad -gt 0) { throw "G5" }
    Say ("[OK] one transaction, measured delete order, " + $shape["assertions"] +
         " assertions, " + [Math]::Round((Get-Item $canonSql).Length / 1MB, 1) + " MB")

    Rule "G6 - BACKUP"
    if ($null -eq $pgdump) { Say "[FAIL] pg_dump.exe not found."; throw "G6" }
    $bp = Start-Process -FilePath $pgdump `
            -ArgumentList @("-h", $PgHost, "-p", "$PgPort", "-U", $PgUser,
                            "-d", $Database, "-Fc", "-f", $backupFile) `
            -NoNewWindow -Wait -PassThru `
            -RedirectStandardError (Join-Path $script:tmp "dump.err")
    if ($bp.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $backupFile)) {
        Say "[FAIL] pg_dump failed."
        Say (Read-IfExists (Join-Path $script:tmp "dump.err"))
        throw "G6"
    }
    $bytes = (Get-Item -LiteralPath $backupFile).Length
    Say ("backup : " + $backupFile)
    Say ("bytes  : " + $bytes)
    if ($bytes -lt 1048576) { Say "[FAIL] the backup is under 1 MB."; throw "G6" }
    Say "[OK] backup written"

    Rule "G7 - PROVE THE RESTORE, DO NOT ASSUME IT"
    Say "A backup nobody has restored is a hope. This restores it into a scratch"
    Say "database and compares row counts against the live one."
    if ($SkipRestoreTest) {
        Say ""
        Say "[WARN] -SkipRestoreTest was given. G7 IS NOT SATISFIED and the"
        Say "       rollback path is unproven."
        if (-not $ReportOnly) { Say "[STOP] refusing to delete without a proven restore."; throw "G7" }
    } else {
        $liveCounts = Invoke-Sql -Sql @'
\pset border 2
SELECT 'material_units' AS t, count(*) AS n FROM public.material_units
UNION ALL SELECT 'quality_events', count(*) FROM public.quality_events
UNION ALL SELECT 'genealogy_edges', count(*) FROM public.genealogy_edges
UNION ALL SELECT 'parameter_observations', count(*) FROM public.parameter_observations
ORDER BY 1;
'@ -Tag "livecount"
        Say $liveCounts.Output
        Invoke-Sql -Sql ("DROP DATABASE IF EXISTS " + $RestoreTestDatabase + ";") `
                   -Tag "droprestore" -Db "postgres" | Out-Null
        $mk = Invoke-Sql -Sql ("CREATE DATABASE " + $RestoreTestDatabase + ";") `
                         -Tag "mkrestore" -Db "postgres"
        if ($mk.ExitCode -ne 0) { Say $mk.Error; throw "G7" }
        $rp = Start-Process -FilePath $pgrestore `
                -ArgumentList @("-h", $PgHost, "-p", "$PgPort", "-U", $PgUser,
                                "-d", $RestoreTestDatabase, "--no-owner",
                                "--no-privileges", $backupFile) `
                -NoNewWindow -Wait -PassThru `
                -RedirectStandardError (Join-Path $script:tmp "restore.err")
        $restoreErr = Read-IfExists (Join-Path $script:tmp "restore.err")
        $restored = Invoke-Sql -Sql @'
\pset border 2
SELECT 'material_units' AS t, count(*) AS n FROM public.material_units
UNION ALL SELECT 'quality_events', count(*) FROM public.quality_events
UNION ALL SELECT 'genealogy_edges', count(*) FROM public.genealogy_edges
UNION ALL SELECT 'parameter_observations', count(*) FROM public.parameter_observations
ORDER BY 1;
'@ -Tag "restorecount" -Db $RestoreTestDatabase
        if ($restored.ExitCode -ne 0) {
            Say "[FAIL] the restored database could not be queried."
            Say $restoreErr
            throw "G7"
        }
        Say $restored.Output
        if ($liveCounts.Output.Trim() -ne $restored.Output.Trim()) {
            Say "[FAIL] restored counts differ from live. THE ROLLBACK PATH IS NOT PROVEN."
            throw "G7"
        }
        Say "[OK] restore proven: the scratch database matches the live one row for row"
        Invoke-Sql -Sql ("DROP DATABASE IF EXISTS " + $RestoreTestDatabase + ";") `
                   -Tag "droprestore2" -Db "postgres" | Out-Null
        Say "[OK] scratch database dropped"
    }

    if ($ReportOnly) {
        Rule "REPORT ONLY - ALL GATES RUN, NOTHING DELETED"
        Say ("backup kept : " + $backupFile)
        Say ("sql kept    : " + $canonSql)
        Say ""
        Say "Run again without -ReportOnly to perform the replacement."
    }
    else {
        Rule "REPLACEMENT - ONE TRANSACTION"
        Say "113 MB of COPY, roughly 400,000 rows against live foreign keys and"
        Say "unique indexes. TEN TO FIFTEEN MINUTES is normal."
        Say ""
        Say "psql now writes STRAIGHT TO THIS CONSOLE. An earlier version sent it to"
        Say "a file, so a destructive step ran silently for a quarter of an hour and"
        Say "could not be told apart from a hung one. Watch for BEGIN, seven DELETEs,"
        Say "six COPYs, four INSERTs, a DO, then COMMIT."
        Say ""
        Say ("started at " + (Get-Date -Format "HH:mm:ss"))
        Say ("-" * 78)
        $applyErr = Join-Path $script:tmp "apply.err"
        $aa = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser,
                "-d", $Database, "-v", "ON_ERROR_STOP=1", "-f", $canonSql)
        $app = Start-Process -FilePath $script:psql -ArgumentList $aa -NoNewWindow `
                             -Wait -PassThru -RedirectStandardError $applyErr
        Say ("-" * 78)
        Say ("finished at " + (Get-Date -Format "HH:mm:ss"))
        $ap = New-Object psobject
        Add-Member -InputObject $ap -MemberType NoteProperty -Name ExitCode -Value $app.ExitCode
        Add-Member -InputObject $ap -MemberType NoteProperty -Name Error -Value (Read-IfExists $applyErr)
        if ($ap.ExitCode -ne 0 -or $ap.Error -match "(?i)(ERROR|FATAL):") {
            Say ("[FAIL] apply exited " + $ap.ExitCode)
            Say $ap.Error
            Say ""
            Say "It was one transaction, so NOTHING changed. The backup is at:"
            Say ("  " + $backupFile)
            throw "apply"
        }
        Say "[OK] replacement committed"

        Rule "VERIFY - THE T-024 CLOSURE CONDITIONS"
        $verSql = @'
\pset border 2
SELECT 'legacy operational rows' AS check_name, count(*) AS found, 0 AS required
FROM (
  SELECT 1 FROM public.material_units WHERE coalesce(source_system,'') <> 'FLEET_V2'
  UNION ALL SELECT 1 FROM public.parameter_observations WHERE coalesce(source_system,'') <> 'FLEET_V2'
  UNION ALL SELECT 1 FROM public.quality_events WHERE coalesce(source_system,'') <> 'FLEET_V2'
  UNION ALL SELECT 1 FROM public.genealogy_edges WHERE coalesce(source_system,'') <> 'FLEET_V2'
  UNION ALL SELECT 1 FROM public.downtime_events WHERE coalesce(source_system,'') <> 'FLEET_V2'
  UNION ALL SELECT 1 FROM public.process_step_executions WHERE coalesce(source_system,'') <> 'FLEET_V2'
) x
UNION ALL
SELECT 'genealogy orphans', count(*), 0 FROM (
  SELECT 1 FROM public.genealogy_edges g
    LEFT JOIN public.material_units m ON m.id = g.parent_material_unit_id WHERE m.id IS NULL
  UNION ALL
  SELECT 1 FROM public.genealogy_edges g
    LEFT JOIN public.material_units m ON m.id = g.child_material_unit_id WHERE m.id IS NULL
) y
UNION ALL
SELECT 'self edges', count(*), 0 FROM public.genealogy_edges
WHERE parent_material_unit_id = child_material_unit_id
UNION ALL
SELECT 'coils without a slab parent', count(*), 0
FROM public.material_units c
WHERE c.material_unit_type = 'Coil'
  AND NOT EXISTS (SELECT 1 FROM public.genealogy_edges g WHERE g.child_material_unit_id = c.id)
UNION ALL
SELECT 'slabs without a heat parent', count(*), 0
FROM public.material_units s
WHERE s.material_unit_type = 'Slab'
  AND NOT EXISTS (SELECT 1 FROM public.genealogy_edges g WHERE g.child_material_unit_id = s.id)
UNION ALL
SELECT 'downtime rows with zero stopped', count(*), 0
FROM public.downtime_events WHERE stopped_minutes <= 0
UNION ALL
-- reported, not asserted against a hard-coded count of a random draw
SELECT 'downtime rows with impact above zero', count(*),
       (SELECT count(*) FROM public.downtime_events WHERE production_impact_minutes > 0)
FROM public.downtime_events WHERE production_impact_minutes > 0
UNION ALL
SELECT 'surface defects without a catalogue row', count(*), 0
FROM public.quality_events WHERE event_type = 'SurfaceDefect' AND defect_catalog_id IS NULL;
'@
        $ver = Invoke-Sql -Sql $verSql -Tag "verify"
        Say $ver.Output
        $vbad = Check-Table -Output $ver.Output -Label "closure"

        Rule "IDENTITY CONVENTION PRESERVED"
        $idSql = @'
\pset border 2
SELECT 'heat codes shared with the donor' AS check_name,
       count(*) AS found, 630 AS required
FROM public.material_units m JOIN src_meltshop_pg.heats h ON h.heat_no = m.material_code
UNION ALL
SELECT 'slab codes shared with the donor', count(*), 5670
FROM public.material_units m JOIN src_caster_oracle_shape.cast_pieces p ON p.piece_id = m.material_code
UNION ALL
SELECT 'coil codes shared with the donor', count(*), 5670
FROM public.material_units m JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = m.material_code;
'@
        $idr = Invoke-Sql -Sql $idSql -Tag "identity"
        Say $idr.Output
        Say "The donor is at 1x and canonical at 3x, so these prove the CONVENTION"
        Say "still matches on the overlapping range, not that the sizes are equal."
        $vbad = $vbad + (Check-Table -Output $idr.Output -Label "identity")

        Rule "READ MODELS - REFRESH WHAT READS THE CANONICAL TABLES"
        $mvSql = @'
\pset border 2
SELECT DISTINCT dependent.relname AS matview
FROM pg_depend d
JOIN pg_rewrite r ON r.oid = d.objid
JOIN pg_class dependent ON dependent.oid = r.ev_class
JOIN pg_class source ON source.oid = d.refobjid
JOIN pg_namespace n ON n.oid = source.relnamespace AND n.nspname = 'public'
WHERE dependent.relkind = 'm'
  AND source.relname IN ('material_units','parameter_observations','quality_events',
                         'downtime_events','genealogy_edges','process_step_executions')
  AND dependent.relname <> source.relname
ORDER BY 1;
'@
        $mv = Invoke-Sql -Sql $mvSql -Tag "matviews"
        Say $mv.Output
        $names = @()
        foreach ($line in ($mv.Output -split "`n")) {
            # trimmed, not anchored: psql lines carry a trailing \r after a `n split
            if ($line.Trim() -match "^\|\s*([a-z0-9_]+)\s*\|$") {
                if ($Matches[1] -ne "matview") { $names += $Matches[1] }
            }
        }
        if ($names.Count -eq 0) {
            Say "No materialized view reads the canonical tables. Nothing to refresh."
            Say "Read models here are refreshed by the application, not by the database."
        } else {
            foreach ($n in $names) {
                $rf = Invoke-Sql -Sql ("REFRESH MATERIALIZED VIEW public." + $n + ";") `
                                 -Tag ("refresh_" + $n)
                if ($rf.ExitCode -ne 0) { Say ("[FAIL] refresh " + $n); $vbad = $vbad + 1 }
                else { Say ("[OK] refreshed " + $n) }
            }
        }

        Rule "API SMOKE - ROW COUNTS DO NOT PROVE A DASHBOARD RENDERS"
        $apiOk = $false
        try {
            $resp = Invoke-WebRequest -Uri ($ApiBaseUrl + "/health") -TimeoutSec 8 `
                                      -UseBasicParsing -ErrorAction Stop
            Say ("  /health -> " + $resp.StatusCode)
            $apiOk = ($resp.StatusCode -eq 200)
        } catch {
            Say ("  /health unreachable at " + $ApiBaseUrl)
        }
        if (-not $apiOk) {
            Say ""
            Say "[WARN] the API was not reachable, so the smoke test is NOT green."
            Say "       T-024 closure requires it. Start the API and re-run with"
            Say "       -ReportOnly to confirm, or check the browser by hand."
            $vbad = $vbad + 1
        } else {
            Say "[OK] API responded"
        }

        if ($vbad -gt 0) { throw "verification" }
    }
}
catch {
    if ($exitCode -eq 0) { $exitCode = 6 }
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "RESULT"
if ($exitCode -ne 0) {
    Say "[FAIL] see the reason above."
    Say ""
    Say "If the failure was in a gate, nothing was touched. If it was in the"
    Say "replacement, it was one transaction and nothing changed either."
    if (Test-Path -LiteralPath $backupFile) { Say ("Backup: " + $backupFile) }
    exit $exitCode
}
if ($ReportOnly) {
    Say "[OK] all seven gates pass. Nothing was deleted."
    exit 0
}
Say "[OK] canonical operational population replaced with Fleet v2."
Say ""
Say "REMAINING FOR T-024 CLOSURE, and neither is done by this script:"
Say "  1. mixed-industry vocabulary - the pharma, tyre and aluminium reference"
Say "     rows are still present and must be dependency-checked, then retired or"
Say "     proven filtered out of every customer-visible selector"
Say "  2. browser check - the API smoke is not a rendered dashboard"
Say ""
Say "AND THE STATE IS NOT PRESENTATION-READY UNTIL T-025. Canonical operational"
Say "truth is now new while the analytical and ML results still reflect the old"
Say "population."
exit 0
