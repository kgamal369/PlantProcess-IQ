#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-024 step A - load the Fleet v2 canonical reference vocabulary into
    ppiq_presentation. ADDITIVE ONLY. Runs with no arguments and asks nothing.

.DESCRIPTION
    THREE DEFECTS OF MINE ARE FIXED HERE.

    D1  THE PASSWORD WAS DECLARED AND NEVER USED. The script took -PgPassword and
        then never set PGPASSWORD, so psql fell back to prompting. Every earlier
        measurement script sets it; this one did not. It now does, and psql is
        given -w so a PROMPT IS FORBIDDEN OUTRIGHT: if the password were ever
        missing again this fails loudly instead of stopping to ask, which is what
        a script meant to run bare should do.

    D2  NO BUILD ASSERTION. Three times running, a stale generator ran a whole
        turn because a browser saved the file as "... (1).py" and Move-Item moved
        nothing. The runner caught it only at the emit step. It now reads the
        generator's own source hash FIRST and, if -ExpectedGeneratorSha is given,
        refuses before opening any database connection.

    D3  THE POWERSHELL 7 TERNARY. An earlier version used ( ) ? : in a file
        declaring #requires -Version 5.1. That directive checks the RUNTIME
        version at execution; it cannot catch syntax the parser rejects first.
        Nothing PowerShell-7-only appears here.

    WHY THIS STEP EXISTS. The deep measurement found the reference tables do not
    carry the Fleet v2 vocabulary: defect_catalogs holds nine codes and not one of
    the fourteen, parameter_definitions holds a steel, pharma and tyre mix, and
    equipment is named EAF_1 rather than EAF-01. Since
    parameter_observations.parameter_definition_id is NOT NULL with RESTRICT, not
    one operational row can be written until this exists.

    ADDITIVE BEFORE DESTRUCTIVE. This inserts and deletes nothing, so its worst
    case is doing less than intended. The operational replacement, which does
    delete, is step B and runs only after this is verified.

.EXAMPLE
    .\tools\run\Invoke-PpiqT024Reference.ps1 -ReportOnly
    .\tools\run\Invoke-PpiqT024Reference.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost               = "127.0.0.1",
    [int]   $PgPort               = 5432,
    [string]$PgUser               = "ppiq_dev",
    [string]$PgPassword           = "ppiq_dev_local_only",
    [string]$Database             = "ppiq_presentation",
    [string]$PsqlPath             = "",
    [string]$ExpectedGeneratorSha = "",
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

function Invoke-PsqlFile {
    param([string]$File, [string]$Tag)
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser,
           "-d", $Database, "-v", "ON_ERROR_STOP=1", "-f", $File, "-o", $o)
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait `
                       -PassThru -RedirectStandardError $e
    $res = New-Object psobject
    Add-Member -InputObject $res -MemberType NoteProperty -Name ExitCode -Value $p.ExitCode
    Add-Member -InputObject $res -MemberType NoteProperty -Name Output   -Value (Read-IfExists $o)
    Add-Member -InputObject $res -MemberType NoteProperty -Name Error    -Value (Read-IfExists $e)
    return $res
}

function Invoke-PsqlQuery {
    param([string]$Sql, [string]$Tag)
    $f = Join-Path $script:tmp ($Tag + ".sql")
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    return Invoke-PsqlFile -File $f -Tag $Tag
}

Rule "PPIQ T-024 STEP A - FLEET V2 REFERENCE VOCABULARY (ADDITIVE)"

$RepoRoot = (Get-Location).Path
$gen = Join-Path $RepoRoot "Backend\tools\generate_fleet_v2_donor.py"
if (-not (Test-Path -LiteralPath $gen)) {
    Say "[FAIL] generator not found at Backend\tools\generate_fleet_v2_donor.py"
    exit 2
}

$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Say "[FAIL] psql.exe not found. Re-run with -PsqlPath."; exit 2 }

Say ("Database : " + $Database)
Say ("User     : " + $PgUser + "   password supplied by the script, never typed")
Say ("psql     : " + $script:psql)

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t024a_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$sqlOut = Join-Path $script:tmp "reference.sql"
$exitCode = 0

try {
    Rule "0 - GENERATOR BUILD"
    $genSha = (Get-FileHash -LiteralPath $gen -Algorithm SHA256).Hash
    Say ("source sha256 : " + $genSha)
    Say ("bytes         : " + (Get-Item -LiteralPath $gen).Length)
    if (-not [string]::IsNullOrWhiteSpace($ExpectedGeneratorSha)) {
        if ($genSha -ne $ExpectedGeneratorSha.ToUpper()) {
            Say ""
            Say "[FAIL] this is not the expected generator build."
            Say ("       expected " + $ExpectedGeneratorSha.ToUpper())
            Say ("       found    " + $genSha)
            Say "[STOP] refusing before opening a database connection."
            throw "build mismatch"
        }
        Say "[OK] build matches the pinned hash"
    } else {
        Say "(no -ExpectedGeneratorSha given: the build is reported, not enforced)"
    }

    Rule "1 - EMIT FROM THE FROZEN GENERATOR"
    $go = Join-Path $script:tmp "gen.out"
    $ge = Join-Path $script:tmp "gen.err"
    $gp = Start-Process -FilePath "python" `
            -ArgumentList @($gen, "--emit", "reference", "--out", $sqlOut) `
            -WorkingDirectory $RepoRoot -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $go -RedirectStandardError $ge
    Say (Read-IfExists $go)
    if ($gp.ExitCode -ne 0) {
        Say "[FAIL] the generator refused to emit."
        Say (Read-IfExists $ge)
        throw "emit failed"
    }

    Rule "2 - PRE-FLIGHT: PROVE THE FILE IS ADDITIVE"
    Say "Checked HERE, not only where it was written. A substring scan is not"
    Say "enough: DELETE appears inside is_deleted and UPDATE inside"
    Say "updated_at_utc. This reads the leading verb of every statement."
    Say ""
    $sqlText = [System.IO.File]::ReadAllText($sqlOut)
    $statements = @($sqlText -split ";") | ForEach-Object { $_.Trim() } |
                  Where-Object { $_ -ne "" }
    $verbs = @{}
    $offenders = @()
    foreach ($st in $statements) {
        $body = ([regex]::Replace($st, "(?m)^\s*--[^\r\n]*\r?\n", "")).Trim()
        if ($body -eq "") { continue }
        $verb = ($body -split "\s+")[0].ToUpper()
        if ($verbs.ContainsKey($verb)) { $verbs[$verb] = $verbs[$verb] + 1 }
        else { $verbs[$verb] = 1 }
        if (@("BEGIN", "INSERT", "COMMIT") -notcontains $verb) { $offenders += $verb }
    }
    foreach ($k in ($verbs.Keys | Sort-Object)) { Say ("  " + $k.PadRight(10) + $verbs[$k]) }
    if ($offenders.Count -gt 0) {
        Say ""
        Say ("[FAIL] statements this step must never carry: " +
             (($offenders | Sort-Object -Unique) -join ", "))
        Say "[STOP] nothing was sent to the database."
        throw "not additive"
    }
    # UNTARGETED guards, because the unique constraint on defect_catalogs is
    # defect_code and not id. An id-targeted guard never fired and the apply
    # failed on INCLUSION, which already existed in the old catalogue.
    $guards = ([regex]::Matches($sqlText, "ON CONFLICT DO NOTHING")).Count
    $inserts = $verbs["INSERT"]
    Say ""
    Say "[OK] only BEGIN, INSERT and COMMIT present"
    Say ("[OK] " + $guards + " of " + $inserts + " inserts carry ON CONFLICT DO NOTHING")
    if ($guards -ne $inserts) {
        Say "[FAIL] an insert has no conflict guard, so a second run would duplicate."
        throw "unguarded insert"
    }

    $countSql = @'
\pset border 2
SELECT 'defect_catalogs' AS table_name, count(*) AS total,
       count(*) FILTER (WHERE source_system = 'FLEET_V2') AS fleet_v2
FROM public.defect_catalogs
UNION ALL
SELECT 'parameter_definitions', count(*),
       count(*) FILTER (WHERE source_system = 'FLEET_V2')
FROM public.parameter_definitions
UNION ALL
SELECT 'equipment', count(*), count(*) FILTER (WHERE source_system = 'FLEET_V2')
FROM public.equipment
ORDER BY 1;
'@

    Rule "3 - COUNTS BEFORE"
    $before = Invoke-PsqlQuery -Sql $countSql -Tag "before"
    if ($before.ExitCode -ne 0) {
        Say "[FAIL] the count query failed."
        Say $before.Error
        throw "count failed"
    }
    Say $before.Output

    if ($ReportOnly) {
        Rule "REPORT ONLY - NOTHING WRITTEN"
        Say ("SQL kept for inspection: " + $sqlOut)
    }
    else {
        Rule "4 - APPLY"
        $apply = Invoke-PsqlFile -File $sqlOut -Tag "apply"
        if ($apply.ExitCode -ne 0 -or $apply.Error -match "(?i)(ERROR|FATAL):") {
            Say ("[FAIL] apply exited " + $apply.ExitCode)
            if (-not [string]::IsNullOrWhiteSpace($apply.Error)) { Say $apply.Error }
            Say ""
            Say "One transaction wrapped every insert, so a failure rolled it back."
            throw "apply failed"
        }
        Say "[OK] applied inside one transaction"

        Rule "5 - COUNTS AFTER"
        $after = Invoke-PsqlQuery -Sql $countSql -Tag "after"
        Say $after.Output

        Rule "6 - GATE: THE VOCABULARY THE OPERATIONAL ROWS NEED"
        $gateSql = @'
\pset border 2
-- The gate asks whether the VOCABULARY EXISTS, not who inserted it. Two codes
-- were already present under the old catalogue's own identifiers, so counting
-- rows labelled FLEET_V2 would fail on a database that is actually correct.
SELECT 'fleet v2 defect codes present' AS check_name, count(*) AS found, 14 AS required
FROM public.defect_catalogs
WHERE defect_code IN ('SCALE','EDGE_CRACK','ROLLED_IN_SCALE','SLIVER','INCLUSION',
                      'PINHOLE','SCRATCH','WAVINESS','CENTRE_BUCKLE','EDGE_WAVE',
                      'ROLL_MARK','LAMINATION','OIL_SPOT','SENSOR_ARTEFACT')
UNION ALL
SELECT 'fleet v2 parameters present', count(*), 29
FROM public.parameter_definitions
WHERE parameter_code IN ('CARBON_PCT','MANGANESE_PCT','SILICON_PCT','SULPHUR_PCT',
    'PHOSPHORUS_PCT','ALUMINIUM_PCT','TAP_TEMP_C','OXYGEN_NM3','POWER_KWH',
    'LF_ARGON_NM3','LF_CALCIUM_M','LF_FINAL_TEMP_C','CASTING_SPEED_MPM',
    'SUPERHEAT_C','MOULD_LEVEL_AVG','FDT_C','CT_C','THICKNESS_MM','WIDTH_MM',
    'ROLL_FORCE_KN','ROLL_GAP_MM','ROLL_SPEED_MPS','ROLL_TEMP_C','ACID_CONC_PCT',
    'BATH_TEMP_C','LINE_SPEED_MPM','QA_WIDTH_MM','QA_THK_MM','QA_ROUGHNESS_UM')
UNION ALL
SELECT 'fleet v2 equipment present', count(*), 18
FROM public.equipment
WHERE equipment_code IN ('EAF-01','EAF-02','LF-01','LF-02','CCM-01','CCM-02',
    'HSM-01','PKL-01','PKL-02','PARSYTEC-01','PARSYTEC-02','HSM-01-F1','HSM-01-F2',
    'HSM-01-F3','HSM-01-F4','HSM-01-F5','HSM-01-F6','HSM-01-F7')
UNION ALL
SELECT 'no operational row touched', count(*), 0
FROM public.material_units WHERE source_system = 'FLEET_V2';
'@
        $gate = Invoke-PsqlQuery -Sql $gateSql -Tag "gate"
        Say $gate.Output
        $fail = 0
        foreach ($line in ($gate.Output -split "`n")) {
            if ($line -match "^\|\s*(.+?)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|") {
                if ([int]$Matches[2] -ne [int]$Matches[3]) {
                    Say ("[FAIL] " + $Matches[1] + ": found " + $Matches[2] +
                         ", required " + $Matches[3])
                    $fail = $fail + 1
                }
            }
        }
        if ($fail -gt 0) { throw "gate failed" }
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
    Say "Nothing was deleted at any point. Re-running is safe - every insert is"
    Say "guarded by ON CONFLICT DO NOTHING."
    exit $exitCode
}
if ($ReportOnly) {
    Say "[OK] pre-flight clean. Nothing was written."
    Say ""
    Say "Run it again without -ReportOnly to apply."
    exit 0
}
Say "[OK] Fleet v2 reference vocabulary is in place."
Say "[OK] No operational row was created, deleted or modified by this step."
Say ""
Say "Step B - the operational replacement - is now unblocked. It is the step"
Say "that deletes, and it runs against a VERIFIED restore in the measured order:"
Say "  risk_scores, quality_events, genealogy_edges, parameter_observations,"
Say "  process_step_executions, downtime_events, then material_units."
Say ""
Say "  git add Backend/tools/generate_fleet_v2_donor.py tools/run/Invoke-PpiqT024Reference.ps1"
Say "  git commit -m ""T-024 step A: Fleet v2 canonical reference vocabulary, additive"""
exit 0
