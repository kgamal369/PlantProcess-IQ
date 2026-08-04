#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-024 step A2 - report and remediate duplicate reference codes created by
    step A. Reports by default; removes only with -Fix.

.DESCRIPTION
    WHAT HAPPENED. Step A inserted 29 parameter definitions. Two of the codes -
    CARBON_PCT and SUPERHEAT_C - already existed in the old catalogue, and unlike
    defect_catalogs, parameter_definitions HAS NO UNIQUE CONSTRAINT ON
    parameter_code. So both inserted and the code now appears twice.

    defect_catalogs behaved correctly: 12 of 14 inserted, INCLUSION and SCRATCH
    were skipped because the unique constraint held. The difference between the
    two tables is the whole finding.

    WHY IT MATTERS. Two CARBON_PCT rows in a parameter picker is the
    mixed-vocabulary problem in miniature, and step B must resolve reference ids
    BY CODE - which is ambiguous while a code maps to two rows.

    HOW THIS REMEDIATES. Not by blind deletion. It first reports, for every
    duplicated code, how many parameter_observations and kpi_parameter_bindings
    reference EACH row. With -Fix it removes only rows that satisfy ALL of:
      - source_system = 'FLEET_V2', so only rows step A created
      - another row with the same parameter_code exists that is NOT FLEET_V2
      - the FLEET_V2 row has ZERO referencing rows anywhere
    Anything failing one of those is reported and left alone.

    The surviving older row keeps its identity, which is what step B will resolve
    to. If its unit or expected range is wrong for Fleet v2, that is a separate
    finding and is reported here rather than silently corrected.

.EXAMPLE
    .\tools\run\Invoke-PpiqT024DedupeReference.ps1
    .\tools\run\Invoke-PpiqT024DedupeReference.ps1 -Fix
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = "",
    [switch]$Fix
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

function Invoke-PsqlQuery {
    param([string]$Sql, [string]$Tag)
    $f = Join-Path $script:tmp ($Tag + ".sql")
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser,
           "-d", $Database, "-v", "ON_ERROR_STOP=1", "-f", $f, "-o", $o)
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait `
                       -PassThru -RedirectStandardError $e
    $res = New-Object psobject
    Add-Member -InputObject $res -MemberType NoteProperty -Name ExitCode -Value $p.ExitCode
    Add-Member -InputObject $res -MemberType NoteProperty -Name Output   -Value (Read-IfExists $o)
    Add-Member -InputObject $res -MemberType NoteProperty -Name Error    -Value (Read-IfExists $e)
    return $res
}

Rule "PPIQ T-024 STEP A2 - DUPLICATE REFERENCE CODES"
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Say "[FAIL] psql.exe not found."; exit 2 }
Say ("Database : " + $Database)
Say ("Mode     : " + $(if ($Fix) { "FIX - will remove qualifying rows" } else { "REPORT ONLY" }))

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t024a2_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$exitCode = 0

try {
    Rule "1 - WHICH REFERENCE TABLES ENFORCE UNIQUENESS ON THEIR CODE?"
    Say "This is the question my measurement never asked, and the reason step A"
    Say "behaved differently on two tables that looked alike."
    Say ""
    $q1 = @'
\pset border 2
SELECT t.relname AS table_name, i.relname AS index_name, pg_get_indexdef(i.oid) AS definition
FROM pg_class t
JOIN pg_namespace n ON n.oid = t.relnamespace AND n.nspname = 'public'
JOIN pg_index ix ON ix.indrelid = t.oid AND ix.indisunique
JOIN pg_class i ON i.oid = ix.indexrelid
WHERE t.relname IN ('defect_catalogs','parameter_definitions','equipment',
                    'operation_definitions')
ORDER BY 1, 2;
'@
    $r1 = Invoke-PsqlQuery -Sql $q1 -Tag "unique"
    if ($r1.ExitCode -ne 0) { Say $r1.Error; throw "query failed" }
    Say $r1.Output

    Rule "2 - DUPLICATED CODES AND WHAT REFERENCES EACH ROW"
    $q2 = @'
\pset border 2
WITH dup AS (
  SELECT parameter_code FROM public.parameter_definitions
  GROUP BY parameter_code HAVING count(*) > 1
)
SELECT pd.parameter_code,
       coalesce(pd.source_system,'(null)') AS source_system,
       pd.id,
       coalesce(pd.unit_of_measure,'(null)') AS unit_of_measure,
       (SELECT count(*) FROM public.parameter_observations o
         WHERE o.parameter_definition_id = pd.id) AS observations,
       (SELECT count(*) FROM public.kpi_parameter_bindings k
         WHERE k.parameter_definition_id = pd.id) AS kpi_bindings
FROM public.parameter_definitions pd
JOIN dup ON dup.parameter_code = pd.parameter_code
ORDER BY pd.parameter_code, pd.source_system NULLS FIRST;
'@
    $r2 = Invoke-PsqlQuery -Sql $q2 -Tag "dups"
    if ($r2.ExitCode -ne 0) { Say $r2.Error; throw "query failed" }
    Say $r2.Output

    Rule "3 - WHAT WOULD BE REMOVED, AND WHAT WOULD NOT"
    Say "A row qualifies only if it is FLEET_V2, another non-FLEET_V2 row carries"
    Say "the same code, and NOTHING references it. Everything else is left alone"
    Say "and reported."
    Say ""
    $q3 = @'
\pset border 2
WITH dup AS (
  SELECT parameter_code FROM public.parameter_definitions
  GROUP BY parameter_code HAVING count(*) > 1
), cand AS (
  SELECT pd.id, pd.parameter_code, pd.source_system,
         (SELECT count(*) FROM public.parameter_observations o
           WHERE o.parameter_definition_id = pd.id)
       + (SELECT count(*) FROM public.kpi_parameter_bindings k
           WHERE k.parameter_definition_id = pd.id) AS refs,
         EXISTS (SELECT 1 FROM public.parameter_definitions o2
                  WHERE o2.parameter_code = pd.parameter_code
                    AND coalesce(o2.source_system,'') <> 'FLEET_V2') AS has_older
  FROM public.parameter_definitions pd
  JOIN dup ON dup.parameter_code = pd.parameter_code
)
SELECT parameter_code, coalesce(source_system,'(null)') AS source_system, refs,
       has_older,
       CASE WHEN source_system = 'FLEET_V2' AND has_older AND refs = 0
            THEN 'REMOVE' ELSE 'KEEP' END AS action
FROM cand ORDER BY parameter_code, action;
'@
    $r3 = Invoke-PsqlQuery -Sql $q3 -Tag "plan"
    if ($r3.ExitCode -ne 0) { Say $r3.Error; throw "query failed" }
    Say $r3.Output

    if (-not $Fix) {
        Rule "REPORT ONLY - NOTHING REMOVED"
        Say "Re-run with -Fix to remove only the rows marked REMOVE above."
    }
    else {
        Rule "4 - REMOVE"
        $q4 = @'
BEGIN;
WITH dup AS (
  SELECT parameter_code FROM public.parameter_definitions
  GROUP BY parameter_code HAVING count(*) > 1
)
DELETE FROM public.parameter_definitions pd
USING dup
WHERE dup.parameter_code = pd.parameter_code
  AND pd.source_system = 'FLEET_V2'
  AND EXISTS (SELECT 1 FROM public.parameter_definitions o2
               WHERE o2.parameter_code = pd.parameter_code
                 AND coalesce(o2.source_system,'') <> 'FLEET_V2')
  AND NOT EXISTS (SELECT 1 FROM public.parameter_observations o
                   WHERE o.parameter_definition_id = pd.id)
  AND NOT EXISTS (SELECT 1 FROM public.kpi_parameter_bindings k
                   WHERE k.parameter_definition_id = pd.id);
COMMIT;
'@
        $r4 = Invoke-PsqlQuery -Sql $q4 -Tag "fix"
        if ($r4.ExitCode -ne 0 -or $r4.Error -match "(?i)(ERROR|FATAL):") {
            Say ("[FAIL] removal exited " + $r4.ExitCode)
            Say $r4.Error
            Say "One transaction wrapped it, so nothing was removed."
            throw "fix failed"
        }
        Say "[OK] removal applied inside one transaction"
    }

    Rule "5 - GATE"
    $q5 = @'
\pset border 2
SELECT 'distinct fleet v2 parameter codes' AS check_name,
       count(DISTINCT parameter_code) AS found, 29 AS required
FROM public.parameter_definitions
WHERE parameter_code IN ('CARBON_PCT','MANGANESE_PCT','SILICON_PCT','SULPHUR_PCT',
    'PHOSPHORUS_PCT','ALUMINIUM_PCT','TAP_TEMP_C','OXYGEN_NM3','POWER_KWH',
    'LF_ARGON_NM3','LF_CALCIUM_M','LF_FINAL_TEMP_C','CASTING_SPEED_MPM',
    'SUPERHEAT_C','MOULD_LEVEL_AVG','FDT_C','CT_C','THICKNESS_MM','WIDTH_MM',
    'ROLL_FORCE_KN','ROLL_GAP_MM','ROLL_SPEED_MPS','ROLL_TEMP_C','ACID_CONC_PCT',
    'BATH_TEMP_C','LINE_SPEED_MPM','QA_WIDTH_MM','QA_THK_MM','QA_ROUGHNESS_UM')
UNION ALL
SELECT 'parameter codes appearing twice', count(*), 0
FROM (SELECT parameter_code FROM public.parameter_definitions
      GROUP BY parameter_code HAVING count(*) > 1) d
UNION ALL
SELECT 'defect codes appearing twice', count(*), 0
FROM (SELECT defect_code FROM public.defect_catalogs
      GROUP BY defect_code HAVING count(*) > 1) d
UNION ALL
SELECT 'equipment codes appearing twice', count(*), 0
FROM (SELECT equipment_code FROM public.equipment
      GROUP BY equipment_code HAVING count(*) > 1) d;
'@
    $r5 = Invoke-PsqlQuery -Sql $q5 -Tag "gate"
    Say $r5.Output
    $fail = 0
    foreach ($line in ($r5.Output -split "`n")) {
        if ($line -match "^\|\s*(.+?)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|") {
            if ([int]$Matches[2] -ne [int]$Matches[3]) {
                Say ("[FAIL] " + $Matches[1] + ": found " + $Matches[2] +
                     ", required " + $Matches[3])
                $fail = $fail + 1
            }
        }
    }
    if ($fail -gt 0 -and $Fix) { throw "gate failed after fix" }
}
catch {
    if ($exitCode -eq 0) { $exitCode = 6 }
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "RESULT"
if ($exitCode -ne 0) {
    Say "[FAIL] see above. Nothing outside the reported plan was touched."
    exit $exitCode
}
if (-not $Fix) {
    Say "[OK] reported. Nothing was changed."
    exit 0
}
Say "[OK] duplicates resolved."
Say ""
Say "Step B must resolve reference ids BY CODE, never by its own computed uuid5,"
Say "because a code that already existed keeps the older row's identity."
exit 0
